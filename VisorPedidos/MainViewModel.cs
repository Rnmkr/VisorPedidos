using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Xml.Linq;
using VisorDataLibrary;

namespace VisorPedidos
{
    public class MainViewModel : INotifyPropertyChanged 
    {
        private List<VisorData> _listaRecibida;
        private List<VisorData> _listaFiltrada;
        private VisorData _datosEnPantalla;
        private List<string> _lineasConfiguradas;
        private string _segundosRotacion;
        private string _ipMulticast;
        private string _puerto;
        private Timer _timer;
        private int _indiceDatosEnPantalla = 0;
        private bool _mostrarParciales;

        public MainViewModel()
        {
            CargarConfiguracion();
            
            IniciarClienteUDP();

            _timer = new Timer(double.Parse(_segundosRotacion + "000"));
            _timer.Elapsed += TimerTick;
            _timer.AutoReset = true;
        }

        public VisorData DatosEnPantalla
        {
            get { return _datosEnPantalla; }
            set
            {
                _datosEnPantalla = value;
                NotifyPropertyChanged();
                AsignarVisibilidadParciales();
            }
        }

        private void AsignarVisibilidadParciales()
        {
            MostrarParciales = (DatosEnPantalla.ParcialTest == DatosEnPantalla.TotalUnidadesPedido) ^
                (DatosEnPantalla.ParcialEmbalado == DatosEnPantalla.TotalUnidadesPedido);
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
                        _listaRecibida = (List<VisorData>)ByteArrayToObject(data);
                        GenerarLista();
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show("Error iniciando cliente UDP." + Environment.NewLine + "No se puede iniciar la aplicación."
                         + Environment.NewLine + e.ToString(), "Visor de Pedidos", MessageBoxButton.OK, MessageBoxImage.Error);
                    Application.Current.Shutdown();

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
                List<VisorData> obj = (List<VisorData>)binForm.Deserialize(memStream);
                return obj;
            }
        }

        /// <summary>
        /// Crea la lista de datos que se van a mostrar en funciion en las lineas configuradas.
        /// </summary>
        private void GenerarLista()
        {
            if (_listaRecibida == null) return;

            _listaFiltrada = new List<VisorData>();

            foreach (var item in _listaRecibida)
            {
                if (_lineasConfiguradas.Contains(item.Linea))
                {
                    _listaFiltrada.Add(item);
                    //REMOVE ITEM CUANDO?
                }
            }

            if (_listaFiltrada == null) return;

            if (_listaFiltrada.Count > 1)
            {
                _timer.Start();
            }
            else
            {
                _timer.Stop();
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
