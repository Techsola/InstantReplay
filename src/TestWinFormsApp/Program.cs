using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Techsola.InstantReplay;

namespace TestWinFormsApp
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            InstantReplayCamera.Start();

            Application.SetCompatibleTextRenderingDefault(false);
            Application.EnableVisualStyles();

            using var mainForm = new Form
            {
                Text = "Test Windows Forms application",
                Controls =
                {
                    new FlowLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        Controls =
                        {
                            CreateButton("File dialog on new thread", ShowDialogOnNewThread),
                            CreateButton("Save current GIF to desktop", Save),
                        },
                    },
                },
                Padding = new(24, 16, 24, 16),
            };

            Application.Run(mainForm);
        }

        private static void ShowDialogOnNewThread()
        {
            var thread = new Thread(() =>
            {
                using var dialog = new OpenFileDialog();

                dialog.ShowDialog(owner: null);
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private static void Save()
        {
            var directoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Techsola.InstantReplay");

            Directory.CreateDirectory(directoryPath);

            var fileName = $"{DateTime.Now:yyyy-MM-dd HH.mm.ss}.gif";
            using var stream = File.Create(Path.Combine(directoryPath, fileName));

            InstantReplayCamera.SaveGif(stream);
        }

        private static Button CreateButton(string text, Action onClick)
        {
            var button = new Button
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlatStyle = FlatStyle.System,
                Text = text,
            };

            button.Click += (_, _) => onClick();

            return button;
        }
    }
}
