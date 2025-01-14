﻿using Microsoft.EntityFrameworkCore.Metadata.Internal;
using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using static System.Net.Mime.MediaTypeNames;

namespace GameVolumeHandler
{
    public partial class frmMain : Form
    {

        private IntPtr _hook;

        string connectionString = @"Data Source=GameVolumeHandler.db;Version=3;";

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public frmMain()
        {
            InitializeComponent();
            HookFocusChange();
            RegisterGlobalHotkey();


            using (var connection = new SQLiteConnection(connectionString))
            {

                connection.Open();

                // Creating a table if it doesn't exist
                string createTableQuery = @"CREATE TABLE IF NOT EXISTS GamesToMonitorVolume (
                                          Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                          ExeName TEXT,
                                          IsActive INTEGER,
                                          Hotkey TEXT
                                        );";
                using (var command = new SQLiteCommand(createTableQuery, connection))
                {
                    command.ExecuteNonQuery();
                }

                string insertTableQuery = @"INSERT INTO GamesToMonitorVolume VALUES (0, 'gears5.exe', 1, 'UNBOUND');";
                using (var command = new SQLiteCommand(insertTableQuery, connection))
                {
                    try
                    {
                        command.ExecuteNonQuery();
                    }
                    catch // ignore dupe entry
                    {
                    }
                    
                }

                LoadDBValuesToGrid();
                //StartProcessMonitoring();
            }
            
        }

        private void HookFocusChange()
        {
            // set up a hook to detect foreground window changes
            WinEventHook.WinEventDelegate procDelegate = new WinEventHook.WinEventDelegate(WinEventCallback);
            _hook = WinEventHook.SetWinEventHook(WinEventHook.EVENT_SYSTEM_FOREGROUND, WinEventHook.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, procDelegate, 0, 0, WinEventHook.WINEVENT_OUTOFCONTEXT);
        }

        private void UnhookFocusChange()
        {
            try
            {
                this.Dispose();
                WinEventHook.UnhookWinEvent(_hook);
            }
            catch (Exception)
            {
                this.Dispose();
            }
            // unhook the event
        }

        private void WinEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            try
            {
                // Check if the foreground window is a game executable or any specific process
                Process foregroundProcess = Process.GetProcessById((int)hwnd);
                if (foregroundProcess != null && foregroundProcess.MainModule != null)
                {
                    string foregroundProcessName = foregroundProcess.MainModule.ModuleName;
                    // Check if the process name matches your game executable
                    if (foregroundProcessName == "Gears5.exe")
                    {
                        // game has gained focus
                        MessageBox.Show("Gears5.exe focused");
                    }
                    else
                    {
                        // Game has lost focus
                        Console.WriteLine("Gears5.exe focused");
                    }
                }
            }
            catch (Exception ex)
            {

                //MessageBox.Show(ex.Message);
            }
            
        }

        private async void btnFileSelect_Click(object sender, EventArgs e)
        {
            string fileName;

            using (var selectFileDialog = new OpenFileDialog())
            {

                selectFileDialog.Filter = "Exe files(*.exe)| *.exe";

                if (selectFileDialog.ShowDialog() == DialogResult.OK)
                {
                    fileName = selectFileDialog.SafeFileName;
                    await InsertAndRefreshValues(fileName);
                }
            }
        }

        private async Task LoadDBValuesToGrid()
        {
            dgvMain.Rows.Clear();

            using (var connection = new SQLiteConnection(connectionString))
            {
                await connection.OpenAsync();

                string selectQuery = "SELECT * FROM GamesToMonitorVolume;";
                using (var command = new SQLiteCommand(selectQuery, connection))
                {
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (await reader.ReadAsync())
                        {
                            DataGridViewRow row = new DataGridViewRow();

                            DataGridViewTextBoxCell cellExe = new DataGridViewTextBoxCell();
                            cellExe.Value = reader["ExeName"].ToString();
                            cellExe.Tag = reader["Id"].ToString();
                            row.Cells.Add(cellExe);

                            DataGridViewTextBoxCell cellActiveStatus = new DataGridViewTextBoxCell();
                            cellActiveStatus.Value = Convert.ToBoolean(reader["IsActive"]);
                            cellActiveStatus.Tag = reader["IsActive"].ToString();
                            row.Cells.Add(cellActiveStatus);

                            DataGridViewTextBoxCell cellHotkey = new DataGridViewTextBoxCell();
                            cellHotkey.Value = reader["Hotkey"].ToString();
                            row.Cells.Add(cellHotkey);

                            DataGridViewTextBoxCell cellDelete = new DataGridViewTextBoxCell();
                            cellDelete.Value = "X";
                            row.Cells.Add(cellDelete);

                            dgvMain.Rows.Add(row);
                            //Console.WriteLine($"Id: {reader["Id"]}, Name: {reader["Name"]}, Age: {reader["Age"]}");
                        }
                    }
                }
            }
        }

        private async Task InsertExeIntoDB(string exeName)
        {
            string tempValidatedExeName = exeName.ToLower().Replace(".exe", "");
            if (exeName == tempValidatedExeName)
            {
                tempValidatedExeName += ".exe";
                exeName = tempValidatedExeName;
            }

            using (var connection = new SQLiteConnection(connectionString))
            {
                await connection.OpenAsync();

                string insertQuery = "INSERT INTO GamesToMonitorVolume (ExeName, IsActive) VALUES (@ExeName, 1);";
                using (var command = new SQLiteCommand(insertQuery, connection))
                {
                    command.Parameters.AddWithValue("@ExeName", exeName);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task DeleteExeFromDB(int Id)
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                await connection.OpenAsync();

                string deleteQuery = $@"DELETE FROM GamesToMonitorVolume WHERE Id = {Id}";
                using (var command = new SQLiteCommand(deleteQuery, connection))
                {
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private void frmMain_Load(object sender, EventArgs e)
        {

        }        

        private async void dgvMain_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int selectedRowIndex = dgvMain.SelectedCells[0].RowIndex;
            int selectedColumnIndex = dgvMain.SelectedCells[0].ColumnIndex;

            object selectedCellValue = dgvMain[selectedColumnIndex, selectedRowIndex].Value;

            try
            {
                if (dgvMain.Columns[selectedColumnIndex].HeaderText == "Delete")
                {
                    // delete option
                    DialogResult mBoxResult = MessageBox.Show($"Remove {dgvMain[0, selectedRowIndex].Value.ToString().Replace(".exe", "") + "?"}", "Really remove this Exe?", MessageBoxButtons.YesNo);
                    if (mBoxResult == DialogResult.Yes)
                    {
                        int id = int.Parse(dgvMain[0, selectedRowIndex].Tag.ToString());
                        await DeleteExeFromDB(id);
                        await LoadDBValuesToGrid();
                    }

                }

                if (dgvMain.Columns[selectedColumnIndex].HeaderText == "Active")
                {
                    // active toggle option

                    int status = int.Parse(dgvMain[selectedColumnIndex, selectedRowIndex].Tag.ToString());

                    //DialogResult mBoxResult = MessageBox.Show($"Toggle status of {dgvMain[0, selectedRowIndex].Value.ToString().Replace(".exe", "") + "?"}", "Toggle this Exe's active status?", MessageBoxButtons.YesNo);
                    //if (mBoxResult == DialogResult.Yes)
                    //{
                        int id = int.Parse(dgvMain[0, selectedRowIndex].Tag.ToString());
                        await ToggleExeStatus(id, status);
                        await LoadDBValuesToGrid();
                    // }

                }

            }
            catch
            {
            }
            
        }

        private async Task ToggleExeStatus(int id, int status)
        {
            if (status == 1)
            {
                status = 0;
            }
            else
            {
                status = 1;
            }
            using (var connection = new SQLiteConnection(connectionString))
            {
                await connection.OpenAsync();

                string updateQuery = $@"UPDATE GamesToMonitorVolume SET IsActive = {status} WHERE
                                        ID = {id};";
                using (var command = new SQLiteCommand(updateQuery, connection))
                {
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            UnhookFocusChange();
            base.OnFormClosing(e);
        }

        private async void btnAddToList_Click(object sender, EventArgs e)
        {
            string fileName = txtExeName.Text.Replace(" ", "");
            await InsertAndRefreshValues(fileName);
        }

        private async Task InsertAndRefreshValues(string fileName)
        {
            if (await CheckIfExeExists(fileName)) return;
            await InsertExeIntoDB(fileName);
            await LoadDBValuesToGrid();
        }

        private async Task<bool> CheckIfExeExists(string exeName)
        {
            string tempValidatedExeName = exeName.ToLower().Replace(".exe", "");
            if (exeName == tempValidatedExeName)
            {
                tempValidatedExeName += ".exe";
                exeName = tempValidatedExeName;
            }

            using (var connection = new SQLiteConnection(connectionString))
            {
                await connection.OpenAsync();

                string checkQuery = "SELECT COUNT(1) FROM GamesToMonitorVolume WHERE ExeName = @ExeName;";
                using (var command = new SQLiteCommand(checkQuery, connection))
                {
                    command.Parameters.AddWithValue("@ExeName", exeName);
                    return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
                }
            }
        }

        private void RegisterGlobalHotkey()
        {
            const int MOD_ALT = 0x0001; // Alt key modifier
            const int MOD_CONTROL = 0x0002; // Control key modifier
            const int VK_M = 0x4D; // M key
            const int HOTKEY_ID = 1; // Identifier for the hotkey

            bool success = RegisterHotKey(this.Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT, VK_M);

            if (!success)
            {
                MessageBox.Show("Failed to register global hotkey.");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            UnregisterHotKey(this.Handle, 1); // HOTKEY_ID
            base.OnFormClosing(e);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_HOTKEY = 0x0312;

            if (m.Msg == WM_HOTKEY)
            {
                int hotkeyId = m.WParam.ToInt32();

                if (hotkeyId == 1) // HOTKEY_ID
                {
                    _ = ToggleExeMute();
                }
            }

            base.WndProc(ref m);
        }

        private async void txtExeName_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                string fileName = txtExeName.Text.Replace(" ", "");
                await InsertAndRefreshValues(fileName);
            }
        }

        private async void btnMute_Click(object sender, EventArgs e)
        {
            await ToggleExeMute();
        }

        private async Task ToggleExeMute()
        {
            var connection = new SQLiteConnection(connectionString);
            await connection.OpenAsync();

            string selectQuery = "SELECT ExeName, IsActive FROM GamesToMonitorVolume;";
            var command = new SQLiteCommand(selectQuery, connection);
            var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string exeName = reader["ExeName"].ToString();
                bool isActive = Convert.ToBoolean(reader["IsActive"]);

                // Mute or unmute executable
                SetApplicationMute(exeName, isActive);           
            }
        }

        private void SetApplicationMute(string exeName, bool mute)
        {
            var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            var sessions = device.AudioSessionManager.Sessions;
            for (int i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];
                if (session.GetProcessID != 0) // Exclude system sounds
                {
                    try
                    {
                        var process = Process.GetProcessById((int)session.GetProcessID);
                        if (process.ProcessName.ToLower().Replace(".exe", "")
                                .Equals(
                                exeName.ToLower().Replace(".exe", ""),
                                StringComparison.OrdinalIgnoreCase
                                )
                           )
                        {
                            session.SimpleAudioVolume.Mute = mute;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Handle processes that may have ended
                        Console.WriteLine($"Error accessing process: {ex.Message}");
                    }
                }
            }
        }

        public enum EDataFlow
        {
            eRender = 0, // Audio rendering (output)
            eCapture = 1, // Audio capture (input)
            eAll = 2, // Audio render and capture
            EDataFlow_enum_count = 3 // Number of data flow types
        }

        // Audio role type
        public enum ERole
        {
            eConsole = 0, // Console sounds
            eMultimedia = 1, // Multimedia playback
            eCommunications = 2, // Voice communications
            ERole_enum_count = 3 // Number of roles
        }
    }    
}
