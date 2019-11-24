﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Threading;
using System.Xml.Linq;
using VisorDataLibrary;

namespace VisorPedidos
{
    public class MainViewModel : INotifyPropertyChanged 
    {
        private VisorData _datosRecibidos;
        private List<VisorData> _listaFiltrada;
        private VisorData _datosEnPantalla;
        private List<string> _lineasConfiguradas;
        private string _segundosRotacion;
        private string _ipMulticast;
        private string _puerto;
        private Timer _timer;
        private int _indiceDatosEnPantalla = 0;
        private bool _mostrarParciales;
        private bool _strikeEmbalado;
        private bool _strikeTesteado;

        public MainViewModel()
        {
            CargarConfiguracion();
            
            IniciarClienteUDP();

            _timer = new Timer(double.Parse(_segundosRotacion + "000"));
            _timer.Elapsed += TimerTick;
            _timer.AutoReset = true;

            _listaFiltrada = new List<VisorData>();
        }

        public bool StrikeEmbalado
        {
            get { return _strikeEmbalado; }
            set
            {
                _strikeEmbalado = value;
                NotifyPropertyChanged();
            }
        }

        public bool StrikeTesteado
        {
            get { return _strikeTesteado; }
            set
            {
                _strikeTesteado = value;
                NotifyPropertyChanged();
            }
        }

        public VisorData DatosEnPantalla
        {
            get { return _datosEnPantalla; }
            set
            {
                _datosEnPantalla = value;
                NotifyPropertyChanged();
                AsignarVisibilidadParciales();
                AsignarStroke();
            }
        }

        private void AsignarStroke()
        {
            if (DatosEnPantalla.UnidadesEmbaladas == DatosEnPantalla.ParcialEmbalado) { StrikeEmbalado = true; } else { StrikeEmbalado = false; }
            if (DatosEnPantalla.UnidadesTesteadas == DatosEnPantalla.ParcialTest) { StrikeTesteado = true; } else { StrikeTesteado = false; }
        }

        private void AsignarVisibilidadParciales()
        {
            if (DatosEnPantalla.ParcialTest != DatosEnPantalla.TotalUnidadesPedido) { MostrarParciales = true; }
            if (DatosEnPantalla.ParcialEmbalado != DatosEnPantalla.TotalUnidadesPedido) { MostrarParciales = true; }
        }

        public string VersionAplicacion
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version.ToString(); }
        }

        public bool MostrarParciales
        {
            get { return _mostrarParciales; }
            set
            {
                _mostrarParciales= value;
                NotifyPropertyChanged();
            }
        }

        private void CargarConfiguracion()
        {
            try
            {
                //string configPath = Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location).ToString(), "VisorPedidos.xml");
                string configPath = @"C:\Users\Rnmkr\Dropbox\repos\VisorPedidos\VisorPedidos\bin\Debug\VisorPedidos.xml"; //solo en design

                XDocument xml = XDocument.Load(configPath);
                IEnumerable<XElement> config = xml.Root.Elements();
                _lineasConfiguradas = new List<string>();
                foreach (var c in config)
                {
                    switch (c.Name.ToString())
                    {
                        case "linea":
                            _lineasConfiguradas.Add(c.Value);
                            break;

                        case "ipmulticast":
                            _ipMulticast = c.Value;
                            break;

                        case "puerto":
                            _puerto = c.Value;
                            break;

                        case "segundosrotacion":
                            _segundosRotacion = c.Value;
                            break;

                        default:
                            throw new Exception("Los valores iniciales son inconsistentes.");
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Error cargando los valores iniciales." + Environment.NewLine + "No se puede iniciar la aplicación."
                     + Environment.NewLine + e.ToString(), "Visor de Pedidos", MessageBoxButton.OK, MessageBoxImage.Error);

                Application.Current.Shutdown();
            }
        }

        /// <summary>
        /// Inicia la recepcion de paquetes UDP.
        /// </summary>
        private async void IniciarClienteUDP()
        {
            await Task.Run(() =>
            {
                try
                {
                    IPAddress multicastIP = IPAddress.Parse(_ipMulticast);
                    IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, Int32.Parse(_puerto));
                    UdpClient udpClient = new UdpClient();
                    udpClient.ExclusiveAddressUse = false;
                    udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    udpClient.Client.Bind(localEndPoint);
                    udpClient.JoinMulticastGroup(multicastIP);

                    while (true)
                    {
                        byte[] data = udpClient.Receive(ref localEndPoint);
                        _datosRecibidos = (VisorData)ByteArrayToObject(data);
                        GenerarLista();
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show("Error en cliente UDP." + Environment.NewLine + "La aplicación se cerrará."
                         + Environment.NewLine + e.ToString(), "Visor de Pedidos", MessageBoxButton.OK, MessageBoxImage.Error);

                    Dispatcher.CurrentDispatcher.Invoke(() =>
                    {
                        Application.Current.Shutdown(); //no funciona
                    });
                }
            });

        }

        /// <summary>
        /// Convierte el array de bytes recibido por UDP a 'struct', usted me entiende.
        /// </summary>
        /// <param name="arrBytes">un array de bytes con un objeto custom que se envio por UDP...</param>
        /// <returns></returns>
        public static Object ByteArrayToObject(byte[] arrBytes)
        {
            using (var memStream = new MemoryStream(arrBytes))
            {
                var binForm = new BinaryFormatter();
                VisorData obj = (VisorData)binForm.Deserialize(memStream);
                return obj;
            }
        }

        /// <summary>
        /// Crea la lista de datos que se van a mostrar en funciion en las lineas configuradas.
        /// </summary>
        private void GenerarLista()
        {
            if (_datosRecibidos == null) return;

            _listaFiltrada.RemoveAll(r => r.PedidoEnProduccion == _datosRecibidos.PedidoAnterior);

            var updateableData = _listaFiltrada.FirstOrDefault(r => r.PedidoEnProduccion == _datosRecibidos.PedidoEnProduccion);

            if (updateableData != null)
            {
                _listaFiltrada[_listaFiltrada.IndexOf(updateableData)] = _datosRecibidos;
            }
            else
            {
                _listaFiltrada.Add(_datosRecibidos);
            }
                     
            if (_listaFiltrada == null) return;


            if (_listaFiltrada.Count > 1)
            {
                _timer.Start();
            }
            else
            {
                _timer.Stop();
                ActualizarPantalla();
            }
        }

        /// <summary>
        /// Cambia los datos que se ven en pantalla.
        /// </summary>
        private void ActualizarPantalla()
        {
            var i = _listaFiltrada.Count;
            
            if (i == 0) return;

            if (_indiceDatosEnPantalla >= i)
            {
                _indiceDatosEnPantalla = 0;
            }

            DatosEnPantalla = _listaFiltrada[_indiceDatosEnPantalla];
            _indiceDatosEnPantalla++;
        }


        private void TimerTick(Object source, ElapsedEventArgs e)
        {
            ActualizarPantalla();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void NotifyPropertyChanged([CallerMemberName]string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
