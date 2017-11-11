using AudioSwitcher.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Security.Cryptography;

namespace AudioSwitcher
{
    public class SysTrayApp : Form
    {
        const string controllerExePath = "EndPointController.exe";

        [STAThread]
        public static void Main(params string[] args)
        {
            if (args.Length > 0 && args[0] == "--update-md5")
            {
                if (System.IO.File.Exists(controllerExePath))
                {
                    CreateControllerExeHash(controllerExePath);
                    MessageBox.Show(Resources.MESSAGE_MD5_UPDATED,
                        Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(Resources.ERROR_CONTROLLER_NOT_FOUND,
                        Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                string errorMessage = "";
                if (!System.IO.File.Exists(controllerExePath))
                {
                    errorMessage = Resources.ERROR_CONTROLLER_NOT_FOUND + "\n" + Resources.MESSAGE_APPLICATION_WILL_BE_CLOSED;
                }
                else if (Settings.Default.EndPointControllerMD5CheckSumExpect == "")
                {
                    errorMessage = Resources.ERROR_NO_MD5;
                }
                else if (!HasCorrectHash(controllerExePath))
                {
                    errorMessage = Resources.ERROR_INCORRECT_HASH + "\n" + Resources.MESSAGE_APPLICATION_WILL_BE_CLOSED;
                }

                if (errorMessage != "")
                {
                    MessageBox.Show(errorMessage, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    Application.Run(new SysTrayApp());
                }
            }
        }

        private NotifyIcon trayIcon;
        private ContextMenu trayMenu;
        private int deviceCount;
        private int currentDeviceId;
        private static List<int> qDevices;

        public SysTrayApp()
        {
            qDevices = Settings.Default.ChoosedDevices ?? new List<int>();
            // Create a simple tray menu
            trayMenu = new ContextMenu();

            // Create a tray icon
            trayIcon = new NotifyIcon();
            trayIcon.Text = "AudioSwitcher";
            trayIcon.Icon = new Icon(Resources.speaker, 40, 40);

            // Add menu to tray icon and show it
            trayIcon.ContextMenu = trayMenu;
            trayIcon.Visible = true;

            // Count sound-devices
            foreach (var tuple in GetDevices())
            {
                deviceCount += 1;
            }

            // Populate device list when menu is opened
            trayIcon.ContextMenu.Popup += PopulateDeviceList;

            // Register MEH on trayicon leftclick
            trayIcon.MouseUp += new MouseEventHandler(TrayIcon_LeftClick);
        }

        #region Program security: EndPointController.exe validation

        private static void CreateControllerExeHash(string controllerExePath)
        {
            Settings.Default.EndPointControllerMD5CheckSumExpect = ComputeMD5Checksum(controllerExePath);
            Settings.Default.Save();
        }

        private static bool HasCorrectHash(string controllerExePath)
        {
            return Settings.Default.EndPointControllerMD5CheckSumExpect == ComputeMD5Checksum(controllerExePath);
        }

        private static string ComputeMD5Checksum(string filePath)
        {
            using (System.IO.FileStream fs = System.IO.File.OpenRead(filePath))
            {
                MD5 md5 = new MD5CryptoServiceProvider();
                byte[] fileData = new byte[fs.Length];
                fs.Read(fileData, 0, (int)fs.Length);
                byte[] checkSum = md5.ComputeHash(fileData);
                string result = BitConverter.ToString(checkSum).Replace("-", String.Empty);
                return result;
            }
        }

        #endregion

        #region Tray events

        // Selects next device in list when trayicon is left-clicked
        private void TrayIcon_LeftClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var cur_id = nextId();
                SelectDevice(cur_id);
                foreach (var tuple in GetDevices().Where(tuple => cur_id == tuple.Item1))
                {
                    trayIcon.Text = Resources.STATUS_PLAYING + " " + tuple.Item2;
                    break;
                }
            }
        }

        public static T NextOf<T>(IList<T> list, T item)
        {
            return list[(list.IndexOf(item) + 1) == list.Count ? 0 : (list.IndexOf(item) + 1)];
        }

        //Gets the ID of the next sound device in the list
        private int nextId()
        {
            if (qDevices.Count > 0) currentDeviceId = NextOf(qDevices, currentDeviceId);
            return currentDeviceId;
        }

        private void PopulateDeviceList(object sender, EventArgs e)
        {
            // Empty menu to prevent stuff to pile up
            trayMenu.MenuItems.Clear();

            // All all active devices
            foreach (var tuple in GetDevices())
            {
                var id = tuple.Item1;
                var deviceName = tuple.Item2;
                var isInUse = qDevices.Contains(id);

                var item = new MenuItem { Checked = isInUse, Text = deviceName + " (" + id + ")" };
                item.Click += (s, a) => AddDeviceToList(id);

                trayMenu.MenuItems.Add(item);
            }

            // Add an exit button
            var exitItem = new MenuItem { Text = Resources.LABEL_EXIT };
            exitItem.Click += OnExit;
            trayMenu.MenuItems.Add("-");
            trayMenu.MenuItems.Add(exitItem);
        }

        private static void AddDeviceToList(int id)
        {
            if (qDevices.Contains(id)) qDevices.Remove(id);
            else qDevices.Add(id);
        }

        #endregion

        #region EndPointController.exe interaction

        private static IEnumerable<Tuple<int, string, bool>> GetDevices()
        {
            var devices = new List<Tuple<int, string, bool>>();

            if (!System.IO.File.Exists(controllerExePath) || !HasCorrectHash(controllerExePath))
            {
                MessageBox.Show(Resources.ERROR_CONTROLLER_CHANGED + "\n" + Resources.MESSAGE_APPLICATION_WILL_BE_CLOSED,
                    Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);

                Settings.Default.ChoosedDevices = qDevices;
                Settings.Default.Save();

                Application.Exit();
            }
            else
            {
                var p = new Process
                {
                    StartInfo =
                                {
                                    UseShellExecute = false,
                                    RedirectStandardOutput = true,
                                    CreateNoWindow = true,
                                    FileName = controllerExePath,
                                    Arguments = "-f \"%d|%ws|%d|%d\""
                                }
                };
                p.Start();
                p.WaitForExit();
                var stdout = p.StandardOutput.ReadToEnd().Trim();

                foreach (var line in stdout.Split('\n'))
                {
                    var elems = line.Trim().Split('|');
                    var deviceInfo = new Tuple<int, string, bool>(int.Parse(elems[0]), elems[1], elems[3].Equals("1"));
                    devices.Add(deviceInfo);
                }
            }

            return devices;
        }

        private static void SelectDevice(int id)
        {
            if (System.IO.File.Exists(controllerExePath) && HasCorrectHash(controllerExePath))
            {
                var p = new Process
                {
                    StartInfo =
                                {
                                    UseShellExecute = false,
                                    RedirectStandardOutput = true,
                                    CreateNoWindow = true,
                                    FileName = controllerExePath,
                                    Arguments = id.ToString(CultureInfo.InvariantCulture)
                                }
                };
                p.Start();
                p.WaitForExit();
            }
            else
            {
                MessageBox.Show(Resources.ERROR_CONTROLLER_CHANGED + "\n" + Resources.MESSAGE_APPLICATION_WILL_BE_CLOSED,
                    Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);

                Settings.Default.ChoosedDevices = qDevices;
                Settings.Default.Save();

                Application.Exit();
            }
        }

        #endregion

        #region Main app methods

        protected override void OnLoad(EventArgs e)
        {
            Visible = false; // Hide form window.
            ShowInTaskbar = false; // Remove from taskbar.

            base.OnLoad(e);
        }

        private void OnExit(object sender, EventArgs e)
        {
            Settings.Default.ChoosedDevices = qDevices;
            Settings.Default.Save();
            Application.Exit();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                // Release the icon resource.
                trayIcon.Dispose();
            }

            base.Dispose(isDisposing);
        }

        #endregion
    }
}
