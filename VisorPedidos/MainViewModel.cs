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
using System.Windows.Media;
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
        private double _segundosRotacion;
        private double _minutosStandBy;
        private string _ipMulticast;
        private string _puerto;
        private Timer _timerRotacion;
        private Timer _timerStandBy;
        private int _indiceDatosEnPantalla = 0; // -1?
        private bool _mostrarParciales;
        private bool _testeoCompletado;
        private bool _embaladoCompletado;
        private bool _pedidoCompletado;
        private bool _mostrarDatos = false;
        private bool _mostrarMensajeStandBy = true;
        private Brush _colorEstadoPedido = Brushes.White;

        public MainViewModel()
        {
            CargarConfiguracion();
            
            IniciarClienteUDP();

            _timerRotacion = new Timer(_segundosRotacion);
            _timerRotacion.Elapsed += TimerRotacionTick;
            _timerRotacion.AutoReset = true;

            _timerStandBy = new Timer(_minutosStandBy);
            _timerStandBy.Elapsed += TimerStandByTick;
            _timerStandBy.AutoReset = true;

            _listaFiltrada = new List<VisorData>();
        }

        public bool PedidoCompletado
        {
            get { return _pedidoCompletado; }
            set
            {
                _pedidoCompletado = value;
                NotifyPropertyChanged();
            }
        }

        public bool EmbaladoCompletado
        {
            get { return _embaladoCompletado; }
            set
            {
                _embaladoCompletado = value;
                NotifyPropertyChanged();
            }
        }

        public Brush ColorEstadoPedido
        {
            get { return _colorEstadoPedido; }
            set
            {
                _colorEstadoPedido = value;
                NotifyPropertyChanged();
            }
        }

        public bool MostrarDatos
        {
            get { return _mostrarDatos; }
            //get { return true; }
            set
            {
                _mostrarDatos = value;
                NotifyPropertyChanged();
            }
        }

        public bool MostrarMensajeStandBy
        {
            get { return _mostrarMensajeStandBy; }
            set
            {
                _mostrarMensajeStandBy = value;
                NotifyPropertyChanged();
            }
        }

        public bool TesteoCompletado
        {
            get { return _testeoCompletado; }
            set
            {
                _testeoCompletado = value;
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
                AsignarVisibilidad();
            }
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

        private void AsignarVisibilidad()
        {
            int embaladas;
            int parcialembalado;
            int testeadas;
            int parcialtesteado;

            try
            {
                embaladas = Convert.ToInt32(DatosEnPantalla.UnidadesEmbaladas);
                parcialembalado = Convert.ToInt32(DatosEnPantalla.ParcialEmbalado);
                testeadas = Convert.ToInt32(DatosEnPantalla.UnidadesTesteadas);
                parcialtesteado = Convert.ToInt32(DatosEnPantalla.ParcialTest);
            }
            catch (Exception)
            {
                return;
            }


            if (embaladas >= parcialembalado)
            {
                DatosEnPantalla.UnidadesEmbaladas = DatosEnPantalla.ParcialEmbalado;
                EmbaladoCompletado = true;
            }
            else
            {
                EmbaladoCompletado = false;
            }

            if (testeadas >= parcialtesteado)
            {
                DatosEnPantalla.UnidadesTesteadas = DatosEnPantalla.ParcialTest;
                TesteoCompletado = true;
            }
            else
            {
                TesteoCompletado = false;
            }


            MostrarParciales = false;
            if (DatosEnPantalla.ParcialTest != DatosEnPantalla.TotalUnidadesPedido) { MostrarParciales = true; }
            if (DatosEnPantalla.ParcialEmbalado != DatosEnPantalla.TotalUnidadesPedido) { MostrarParciales = true; }

            switch (_datosEnPantalla.EstadoPedido)
            {
                case "FALTANTES":
                    ColorEstadoPedido = Brushes.Red;
                    PedidoCompletado = false;
                    break;

                case "EN PROCESO":
                    ColorEstadoPedido = Brushes.Lime;
                    if (TesteoCompletado == true && EmbaladoCompletado == true)
                    {
                        PedidoCompletado = true;
                    }
                    else
                    {
                        PedidoCompletado = false;
                    }
                    break;

                default:
                    ColorEstadoPedido = Brushes.White;
                    break;
            }

        }

        private void CargarConfiguracion()
        {
            try
            {
                string configPath = Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location).ToString(), "VisorPedidos.xml");

                if (!File.Exists(configPath))
                {
                    File.WriteAllText(configPath, VisorPedidos.Properties.Resources.VisorPedidos);
                }


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

                        case "ip":
                            _ipMulticast = c.Value;
                            break;

                        case "puerto":
                            _puerto = c.Value;
                            break;

                        case "segundosrotacion":
                            var sr = double.Parse(c.Value);
                            _segundosRotacion = sr * 1000;
                            break;

                        case "minutosstandby":
                            var mr = double.Parse(c.Value);
                            _minutosStandBy = mr * 60000;
                            break;

                        default:
                            throw new Exception("Los valores iniciales son inconsistentes.");
                    }
                }
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show("Error cargando los valores iniciales." + Environment.NewLine + "No se puede iniciar la aplicación."
                     + Environment.NewLine + e.ToString(), "Visor de Pedidos", MessageBoxButton.OK, MessageBoxImage.Error);

                System.Windows.Application.Current.Shutdown();
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
                    System.Windows.MessageBox.Show("Error en cliente UDP." + Environment.NewLine + "La aplicación se cerrará."
                         + Environment.NewLine + e.ToString(), "Visor de Pedidos", MessageBoxButton.OK, MessageBoxImage.Error);

                    Dispatcher.CurrentDispatcher.Invoke(() =>
                    {
                        System.Windows.Application.Current.Shutdown(); //no funciona
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
            if (!_lineasConfiguradas.Contains(_datosRecibidos.Linea)) return;
            if (!ComprobarDatosNull()) return;

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
                _timerRotacion.Start();
            }
            else
            {
                _timerRotacion.Stop();
                ActualizarPantalla();
            }

            MostrarDatos = true;
            MostrarMensajeStandBy = false;
            _timerStandBy.Stop();
            _timerStandBy.Start();
        }

        private bool ComprobarDatosNull()
        {
            bool b = true;
            if (string.IsNullOrWhiteSpace(_datosRecibidos.Linea)) { b = false; }
            if (string.IsNullOrWhiteSpace(_datosRecibidos.ParcialEmbalado)) { b = false; }
            if (string.IsNullOrWhiteSpace(_datosRecibidos.ParcialTest)) { b = false; }
            if (string.IsNullOrWhiteSpace(_datosRecibidos.PedidoEnProduccion)) { b = false; }
            if (string.IsNullOrWhiteSpace(_datosRecibidos.Supervisor)) { b = false; }
            if (string.IsNullOrWhiteSpace(_datosRecibidos.TotalUnidadesPedido)) { b = false; }
            if (string.IsNullOrWhiteSpace(_datosRecibidos.UnidadesEmbaladas)) { b = false; }
            if (string.IsNullOrWhiteSpace(_datosRecibidos.UnidadesTesteadas)) { b = false; }
            if (_datosRecibidos.UnidadesTesteadas == "0") { b = false; }
            return b;
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
                _indiceDatosEnPantalla = 0; // -1?
            }

            DatosEnPantalla = _listaFiltrada[_indiceDatosEnPantalla];
            _indiceDatosEnPantalla++;
        }

        private void TimerRotacionTick(Object source, ElapsedEventArgs e)
        {
            ActualizarPantalla();
        }

        private void TimerStandByTick(Object source, ElapsedEventArgs e)
        {
            SetStandBy();
        }

        private void SetStandBy()
        {
            _timerRotacion.Stop();
            _timerStandBy.Stop();
            MostrarDatos = false;
            MostrarMensajeStandBy = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void NotifyPropertyChanged([CallerMemberName]string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
