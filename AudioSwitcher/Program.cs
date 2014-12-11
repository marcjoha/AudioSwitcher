using AudioSwitcher.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace AudioSwitcher
{
    public class SysTrayApp : Form
    {
        [STAThread]
        public static void Main()
        {
            Application.Run(new SysTrayApp());
        }

        private NotifyIcon trayIcon;
        private ContextMenu trayMenu;
        private int deviceCount;
        private int currentDeviceId;
        

        public SysTrayApp()
        {
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

        // Selects next device in list when trayicon is left-clicked
        private void TrayIcon_LeftClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                SelectDevice(nextId());
            }
        }

        //Gets the ID of the next sound device in the list
        private int nextId()
        {
            if (currentDeviceId == deviceCount){
                currentDeviceId = 1;
            } else {
                currentDeviceId += 1;
            }
            return currentDeviceId;
        }

        

        #region Tray events

        private void PopulateDeviceList(object sender, EventArgs e)
        {
            // Empty menu to prevent stuff to pile up
            trayMenu.MenuItems.Clear();

            // All all active devices
            foreach (var tuple in GetDevices())
            {
                var id = tuple.Item1;
                var deviceName = tuple.Item2;
                var isInUse = tuple.Item3;

                var item = new MenuItem {Checked = isInUse, Text = deviceName};
                item.Click += (s, a) => SelectDevice(id);

                trayMenu.MenuItems.Add(item);
            }

            // Add an exit button
            var exitItem = new MenuItem {Text = "Exit"};
            exitItem.Click += OnExit;
            trayMenu.MenuItems.Add("-");
            trayMenu.MenuItems.Add(exitItem);
        }

        #endregion

        #region EndPointController.exe interaction

        private static IEnumerable<Tuple<int, string, bool>> GetDevices()
        {
            var p = new Process
                        {
                            StartInfo =
                                {
                                    UseShellExecute = false,
                                    RedirectStandardOutput = true,
                                    CreateNoWindow = true,
                                    FileName = "EndPointController.exe",
                                    Arguments = "-f \"%d|%ws|%d|%d\""
                                }
                        };
            p.Start();
            p.WaitForExit();
            var stdout = p.StandardOutput.ReadToEnd().Trim();

            var devices = new List<Tuple<int, string, bool>>();

            foreach (var line in stdout.Split('\n'))
            {
                var elems = line.Trim().Split('|');
                var deviceInfo = new Tuple<int, string, bool>(int.Parse(elems[0]), elems[1], elems[3].Equals("1"));
                devices.Add(deviceInfo);
            }

            return devices;
        }

        private static void SelectDevice(int id)
        {
            var p = new Process
                        {
                            StartInfo =
                                {
                                    UseShellExecute = false,
                                    RedirectStandardOutput = true,
                                    CreateNoWindow = true,
                                    FileName = "EndPointController.exe",
                                    Arguments = id.ToString(CultureInfo.InvariantCulture)
                                }
                        };
            p.Start();
            p.WaitForExit();
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