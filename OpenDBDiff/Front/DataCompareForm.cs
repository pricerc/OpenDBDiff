using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using OpenDBDiff.Schema.Model;
using System.Linq;
using System.Text;

namespace OpenDBDiff.Front
{
    public partial class DataCompareForm : Form
    {
        public DataCompareForm(ISchemaBase Selected, string SrcConnectionString, string DestConnectionString)
        {
            InitializeComponent();
            this.selected = Selected;
            this.srcConnectionString = SrcConnectionString;
            this.destConnectionString = DestConnectionString;

            doCompare();
        }

        private void doCompare()
        {
            DataTable srcTable = Updater.getData(selected, srcConnectionString);
            DataTable destTable = Updater.getData(selected, destConnectionString);

            srcDgv.MultiSelect = false;
            srcDgv.ReadOnly = true;
            srcDgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            srcDgv.RowHeadersVisible = false;
            srcDgv.DataSource = srcTable;
            srcDgv.Rows[0].Cells[0].Style.ForeColor = Color.Blue;

            destDgv.MultiSelect = false;
            destDgv.ReadOnly = true;
            destDgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            destDgv.RowHeadersVisible = false;
            destDgv.DataSource = destTable;
            destDgv.CellFormatting += new DataGridViewCellFormattingEventHandler(destDgv_CellFormatting);

            srcDgv.DataError += new DataGridViewDataErrorEventHandler(dgv_DataError);
            destDgv.DataError += new DataGridViewDataErrorEventHandler(dgv_DataError);

            if (FixTimestampColumns(srcDgv) > 0)
                srcDgv.CellFormatting +=
                        new DataGridViewCellFormattingEventHandler(binary_CellFormatting);

            if (FixTimestampColumns(destDgv) > 0)
                destDgv.CellFormatting +=
                        new DataGridViewCellFormattingEventHandler(binary_CellFormatting);

        }

        private int FixTimestampColumns(DataGridView grid)
        {
            var table = (DataTable)grid.DataSource;

            // get readonly byte[] columns; they're *probably* a timestamp.
            var victims = grid.Columns.OfType<DataGridViewImageColumn>()
                .Where(gc => table.Columns[gc.DataPropertyName].ReadOnly)
                .ToList();

            foreach (var victim in victims)
            {
                grid.Columns.Remove(victim);
                grid.Columns.Insert(victim.Index, new DataGridViewTextBoxColumn()
                {
                    ReadOnly = true
                    ,
                    DataPropertyName = victim.DataPropertyName
                    ,
                    Name = victim.Name
                });
            }
            return victims.Count;
        }

        private void dgv_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            // handle errors a bit more elegantly than the default.
            System.Diagnostics.Debug.Print(e.Exception.ToString());
        }

        private void binary_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var grid = (DataGridView)sender;
            if (!(grid.Columns[e.ColumnIndex] is DataGridViewTextBoxColumn)) return;
            if (grid.Columns[e.ColumnIndex].ValueType != typeof(byte[])) return;

            var table = (DataTable)grid.DataSource;
            if (e.RowIndex >= table.Rows.Count) return;

            e.Value = FormatByteArray((byte[])e.Value, 64);
            e.FormattingApplied = true;
            return;
        }

        private static object FormatByteArray(byte[] data, int maxLength)
        {
            var result = new StringBuilder(maxLength);
            for (int i = 0; i < maxLength && i < data.Length; i++)
            {
                result.Append(data[i].ToString("X2"));
            }
            return result.ToString();
        }

        private void destDgv_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            DataTable table = (DataTable)destDgv.DataSource;
            if (e.RowIndex < table.Rows.Count)
            {
                if (table.Rows[e.RowIndex].RowState == DataRowState.Added)
                {
                    e.CellStyle.ForeColor = Color.Green;
                }
                else if (table.Rows[e.RowIndex].RowState == DataRowState.Modified)
                {
                    e.CellStyle.ForeColor = Color.Blue;
                }
            }
        }

        private void btnCommitChanges_Click(object sender, EventArgs e)
        {
            DataTable destination = (DataTable)destDgv.DataSource;
            DataTable edits = destination.GetChanges();
            if (Updater.CommitTable(edits, selected.FullName, destConnectionString))
            {
                destination.AcceptChanges();
                doCompare();
                btnCommitChanges.Enabled = false;
            }
        }

        private void btnUpdateRow_Click(object sender, EventArgs e)
        {
            DataTable source = (DataTable)srcDgv.DataSource;
            DataTable destination = (DataTable)destDgv.DataSource;

            object[] sourceItems = source.Rows[srcDgv.CurrentRow.Index].ItemArray;

            for (int i = 0; i < destination.Columns.Count; i++)
            {
                if (destination.Columns[i].Unique)
                {
                    if (destination.Rows.Find(sourceItems[i]) == null && destination.Columns[i].AutoIncrement)
                    {
                        sourceItems[i] = null;
                    }
                }
            }

            destination.BeginLoadData();
            destination.LoadDataRow(sourceItems, false);
            destination.EndLoadData();
            btnCommitChanges.Enabled = true;
        }

        private void btnMerge_Click(object sender, EventArgs e)
        {
            DataTable source = (DataTable)srcDgv.DataSource;
            DataTable destination = (DataTable)destDgv.DataSource;

            destination.Merge(source, true);
            foreach (DataRow dr in destination.Rows)
            {
                if (dr.RowState == DataRowState.Unchanged)
                {
                    dr.SetAdded();
                }
            }
            btnCommitChanges.Enabled = true;
        }

        private void btnRowToRow_Click(object sender, EventArgs e)
        {
            DataTable source = (DataTable)srcDgv.DataSource;
            DataTable destination = (DataTable)destDgv.DataSource;

            DataRow sourceRow = source.Rows[srcDgv.CurrentRow.Index];
            DataRow destinationRow = destination.Rows[destDgv.CurrentRow.Index];

            for (int i = 0; i < destination.Columns.Count; i++)
            {
                if (!destination.Columns[i].Unique)
                {
                    destinationRow[i] = sourceRow[i];
                }
            }
            btnCommitChanges.Enabled = true;
        }
        private ISchemaBase selected;
        private string srcConnectionString;
        private string destConnectionString;
    }
}
