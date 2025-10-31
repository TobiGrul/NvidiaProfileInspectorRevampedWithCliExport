﻿using nspector.Common;
using nspector.Common.Helper;
using nspector.Native.WINAPI;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace nspector
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                // Remove Zone.Identifier from Alternate Data Stream
                SafeNativeMethods.DeleteFile(Application.ExecutablePath + ":Zone.Identifier");
            }
            catch { }
#if RELEASE
            try
            {
#endif
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            DropDownMenuScrollWheelHandler.Enable(true);

            var argWriteFileIndex = ArgWriteFileIndex(args);

            if (argWriteFileIndex != -1 && ArgExists(args, "-silentExport"))
            {
                var export = DrsServiceLocator.ImportService;
                var profileScanner = DrsServiceLocator.ScannerService;
                var scannerCancelationTokenSource = new CancellationTokenSource();
                var progressHandler = new Progress<int>(value => { });
                profileScanner.ScanProfileSettingsAsync(true, progressHandler, scannerCancelationTokenSource.Token).GetAwaiter().GetResult();

                export.ExportProfiles(DrsServiceLocator.ScannerService.ModifiedProfiles, args[argWriteFileIndex], false);
            }
            else
            {
                var argFileIndex = ArgFileIndex(args);
                if (argFileIndex != -1)
                {
                    if (new FileInfo(args[argFileIndex]).Extension.ToLowerInvariant() == ".nip")
                    {
                        try
                        {
                            var import = DrsServiceLocator.ImportService;
                            var importReport = import.ImportProfiles(args[argFileIndex]);
                            GC.Collect();
                            Process current = Process.GetCurrentProcess();
                            foreach (
                                Process process in
                                    Process.GetProcessesByName(current.ProcessName.Replace(".vshost", "")))
                            {
                                if (process.Id != current.Id && process.MainWindowTitle.Contains("Settings"))
                                {
                                    MessageHelper mh = new();
                                    mh.sendWindowsStringMessage((int)process.MainWindowHandle, 0, "ProfilesImported");
                                }
                            }

                            if (string.IsNullOrEmpty(importReport) && !ArgExists(args, "-silentImport") && !ArgExists(args, "-silent"))
                            {
                                frmDrvSettings.ShowImportDoneMessage(importReport);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Import Error: " + ex.Message, Application.ProductName + " Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
                else if (ArgExists(args, "-createCSN"))
                {
                    File.WriteAllText("CustomSettingNames.xml", Properties.Resources.CustomSettingNames);
                }
                else
                {
                    bool createdNew = true;
                    using Mutex mutex = new(true, Application.ProductName, out createdNew);
                    if (createdNew)
                    {
                        Application.Run(new frmDrvSettings(ArgExists(args, "-showOnlyCSN"), ArgExists(args, "-disableScan")));
                    }
                    else
                    {
                        Process current = Process.GetCurrentProcess();
                        foreach (
                            Process process in
                                Process.GetProcessesByName(current.ProcessName.Replace(".vshost", "")))
                        {
                            if (process.Id != current.Id && process.MainWindowTitle.Contains("Settings"))
                            {
                                MessageHelper mh = new();
                                mh.bringAppToFront((int)process.MainWindowHandle);
                            }
                        }
                    }
                }
            }
#if RELEASE
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n\r\n" + ex.StackTrace, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
#endif
        }

        private static bool ArgExists(string[] args, string arg)
        {
            foreach (string a in args)
            {
                if (a.ToUpper() == arg.ToUpper())
                    return true;
            }
            return false;
        }

        private static int ArgFileIndex(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (File.Exists(args[i]))
                    return i;
            }

            return -1;
        }

        private static int ArgWriteFileIndex(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string filepath = args[i];
                if (filepath.EndsWith(".nip"))
                    return i;
            }

            return -1;
        }
    }
}
