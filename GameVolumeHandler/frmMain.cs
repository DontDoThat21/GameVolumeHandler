﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
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

        public frmMain()
        {
            InitializeComponent();
            HookFocusChange();


            using (var connection = new SQLiteConnection(connectionString))
            {

                connection.Open();

                // Creating a table if it doesn't exist
                string createTableQuery = @"CREATE TABLE IF NOT EXISTS GamesToMonitorVolume (
                                          Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                          ExeName TEXT,
                                          IsActive INTEGER
                                        );";
                using (var command = new SQLiteCommand(createTableQuery, connection))
                {
                    command.ExecuteNonQuery();
                }

                string insertTableQuery = @"INSERT INTO GamesToMonitorVolume VALUES (0, 'Gears5.exe', 1);";
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
            // unhook the event
            WinEventHook.UnhookWinEvent(_hook);
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

        private void btnFileSelect_Click(object sender, EventArgs e)
        {
            string fileName;

            using (var selectFileDialog = new OpenFileDialog())
            {

                selectFileDialog.Filter = "Exe files(*.exe)| *.exe";

                if (selectFileDialog.ShowDialog() == DialogResult.OK)
                {
                    fileName = selectFileDialog.SafeFileName;
                    InsertExeIntoDB(fileName);
                    LoadDBValuesToGrid();
                }
            }
        }

        private void LoadDBValuesToGrid()
        {
            dgvMain.Rows.Clear();

            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();

                string selectQuery = "SELECT * FROM GamesToMonitorVolume;";
                using (var command = new SQLiteCommand(selectQuery, connection))
                {
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            DataGridViewRow row = new DataGridViewRow();

                            DataGridViewTextBoxCell cellExe = new DataGridViewTextBoxCell();
                            cellExe.Value = reader["ExeName"].ToString();
                            cellExe.Tag = reader["Id"].ToString();
                            row.Cells.Add(cellExe);

                            DataGridViewTextBoxCell cellActiveStatus = new DataGridViewTextBoxCell();
                            cellActiveStatus.Value = reader["IsActive"].ToString();
                            row.Cells.Add(cellActiveStatus);

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

        private void InsertExeIntoDB(string ExeName)
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();

                string insertQuery = $@"INSERT INTO GamesToMonitorVolume (ExeName, IsActive) VALUES ('{ExeName}', 1);";
                using (var command = new SQLiteCommand(insertQuery, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        private void DeleteExeFromDB(int Id)
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();

                string deleteQuery = $@"DELETE FROM GamesToMonitorVolume WHERE Id = {Id}";
                using (var command = new SQLiteCommand(deleteQuery, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        private void frmMain_Load(object sender, EventArgs e)
        {

        }        

        private void dgvMain_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int selectedRowIndex = dgvMain.SelectedCells[0].RowIndex;
            int selectedColumnIndex = dgvMain.SelectedCells[0].ColumnIndex;

            object selectedCellValue = dgvMain[selectedColumnIndex, selectedRowIndex].Value;

            try
            {
                if (dgvMain.Columns[selectedColumnIndex].HeaderText == "Delete")
                {
                    // delete option
                    DialogResult mBoxResult = MessageBox.Show($"Delete {dgvMain[0, selectedRowIndex].Value.ToString().Replace(".exe", "") + "?"}", "Really remove this Exe?", MessageBoxButtons.YesNo);
                    if (mBoxResult == DialogResult.Yes)
                    {
                        int id = int.Parse(dgvMain[0, selectedRowIndex].Tag.ToString());
                        DeleteExeFromDB(id);
                        LoadDBValuesToGrid();
                    }

                }

                if (dgvMain.Columns[selectedColumnIndex].HeaderText == "Active")
                {
                    // active toggle option

                    int status = int.Parse(dgvMain[selectedColumnIndex, selectedRowIndex].Value.ToString());

                    DialogResult mBoxResult = MessageBox.Show($"Toggle status of {dgvMain[0, selectedRowIndex].Value.ToString().Replace(".exe", "") + "?"}", "Toggle this Exe's active status?", MessageBoxButtons.YesNo);
                    if (mBoxResult == DialogResult.Yes)
                    {
                        int id = int.Parse(dgvMain[0, selectedRowIndex].Tag.ToString());
                        ToggleExeStatus(id, status);
                        LoadDBValuesToGrid();
                    }

                }

            }
            catch
            {
            }
            
        }

        private void ToggleExeStatus(int id, int status)
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
                connection.Open();

                string updateQuery = $@"UPDATE GamesToMonitorVolume SET IsActive = {status} WHERE
                                        ID = {id};";
                using (var command = new SQLiteCommand(updateQuery, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            UnhookFocusChange();
            base.OnFormClosing(e);
        }
    }    
}
