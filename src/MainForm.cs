using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MacChanger
{
    public sealed class MainForm : Form
    {
        private const int MaxVendorsShown = 800;

        private List<AdapterInfo> _adapters = new List<AdapterInfo>();
        private bool _busy;
        private bool _suppressVendorSel;

        private ComboBox _cmbAdapter;
        private TextBox _txtAdapterFilter;
        private Label _lblAdapterCount;
        private Button _btnBatch;
        private ContextMenuStrip _batchMenu;
        private CheckBox _chkVirtual;
        private CheckBox _chkUpOnly;
        private Button _btnRefresh;
        private GroupBox _grpInfo;
        private Label _lblDescription, _lblConnection, _lblType, _lblState;
        private Label _lblCurrentMac, _lblOriginalMac, _lblOverride, _lblInfoHeader;
        private TableLayoutPanel _infoTable;
        private TextBox _txtMac;
        private Label _lblMacState;
        private Button _btnRandom;
        private TextBox _txtVendorSearch;
        private ComboBox _cmbVendor;
        private Label _lblVendorCount;
        private CheckBox _chkAuto02;
        private RadioButton _rbPersistent, _rbTemp;
        private CheckBox _chkRestartNow;
        private Button _btnApply, _btnRestore, _btnHelp;
        private TextBox _txtLog;
        private Label _lblSt, _lblDbInfo;

        public MainForm()
        {
            BuildUi();
            VendorDb.Load();
            ShopVendors("");
            UpdateDbLabel();
            RefreshAdaptersAsync();
        }

        private static Label NewLabel(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = false;
            l.SetBounds(0, 0, 100, 20);
            return l;
        }

        private static GroupBox NewGroup(string title, int x, int y, int w, int h)
        {
            GroupBox g = new GroupBox();
            g.Text = title;
            g.SetBounds(x, y, w, h);
            return g;
        }

        private void BuildUi()
        {
            Text = "MAC Address Changer - freeware";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(650, 720);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            Icon = null;

            Label adapterCaption = NewLabel("Network adapter:");
            adapterCaption.SetBounds(12, 20, 88, 16);
            Controls.Add(adapterCaption);

            _cmbAdapter = new ComboBox();
            _cmbAdapter.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbAdapter.Location = new Point(102, 17);
            _cmbAdapter.Size = new Size(436, 22);
            _cmbAdapter.SelectedIndexChanged += OnAdapterSelected;
            Controls.Add(_cmbAdapter);

            _btnRefresh = new Button();
            _btnRefresh.Text = "Refresh";
            _btnRefresh.SetBounds(548, 15, 88, 24);
            _btnRefresh.Click += delegate { RefreshAdaptersAsync(); };
            Controls.Add(_btnRefresh);

            Label filterCaption = NewLabel("Filter list:");
            filterCaption.SetBounds(12, 48, 88, 16);
            Controls.Add(filterCaption);

            _txtAdapterFilter = new TextBox();
            _txtAdapterFilter.SetBounds(102, 45, 436, 21);
            _txtAdapterFilter.TextChanged += OnAdapterFilterChanged;
            Controls.Add(_txtAdapterFilter);

            _lblAdapterCount = NewLabel("");
            _lblAdapterCount.SetBounds(548, 48, 88, 16);
            _lblAdapterCount.ForeColor = Color.DimGray;
            _lblAdapterCount.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(_lblAdapterCount);

            ToolTip ttFilter = new ToolTip();
            ttFilter.SetToolTip(_txtAdapterFilter,
                "Type part of a name, a MAC address or a status to narrow the list:\n" +
                "'wi-fi', 'realtek', '00:1b', 'up', 'virtual'.");

            _chkVirtual = new CheckBox();
            _chkVirtual.Text = "Include hidden / virtual adapters";
            _chkVirtual.SetBounds(102, 72, 348, 18);
            _chkVirtual.CheckedChanged += delegate
            {
                if (!_busy) RefreshAdaptersAsync();
            };
            ToolTip ttVirtual = new ToolTip();
            ttVirtual.SetToolTip(_chkVirtual,
                "Also lists emulated adapters: WAN Miniports, Microsoft Wi-Fi Direct,\n" +
                "TAP / vEthernet / Wintun / VPN adapters, KM-TEST loopback, and\n" +
                "disabled or detached virtual devices.");
            Controls.Add(_chkVirtual);

            _chkUpOnly = new CheckBox();
            _chkUpOnly.Text = "Only show up adapters";
            _chkUpOnly.SetBounds(456, 72, 180, 18);
            _chkUpOnly.CheckedChanged += delegate
            {
                if (!_busy) RefreshAdaptersAsync();
            };
            ToolTip ttUp = new ToolTip();
            ttUp.SetToolTip(_chkUpOnly,
                "Lists only adapters Windows reports as up (connected).\n" +
                "Disabled, disconnected and not-present adapters are hidden.\n" +
                "Combine with 'Include hidden/virtual adapters' to include\n" +
                "up virtual adapters such as WAN Miniports.");
            Controls.Add(_chkUpOnly);

            _grpInfo = NewGroup("Adapter information", 12, 96, 624, 196);

            _lblInfoHeader = NewLabel("");
            _lblInfoHeader.SetBounds(10, 20, 400, 14);
            _lblInfoHeader.ForeColor = Color.FromArgb(40, 90, 160);
            _grpInfo.Controls.Add(_lblInfoHeader);

            _infoTable = new TableLayoutPanel();
            _infoTable.SetBounds(10, 36, 596, 150);
            _infoTable.ColumnCount = 2;
            _infoTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
            _infoTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _infoTable.CellBorderStyle = TableLayoutPanelCellBorderStyle.None;

            _lblDescription = new Label(); _lblConnection = new Label(); _lblType = new Label();
            _lblState = new Label(); _lblCurrentMac = new Label();
            _lblOriginalMac = new Label(); _lblOverride = new Label();

            AddInfoRow(_lblDescription, "Description:", 0);
            AddInfoRow(_lblConnection, "Connection:", 1);
            AddInfoRow(_lblType, "Type:", 2);
            AddInfoRow(_lblState, "State:", 3);
            AddInfoRow(_lblCurrentMac, "Current MAC:", 4);
            AddInfoRow(_lblOriginalMac, "Original MAC:", 5);
            AddInfoRow(_lblOverride, "Negotiation:", 6);
            _grpInfo.Controls.Add(_infoTable);
            Controls.Add(_grpInfo);

            GroupBox grpNew = NewGroup("New MAC address", 12, 296, 624, 220);

            Label macCaption = NewLabel("Address:");
            macCaption.SetBounds(14, 30, 60, 16);
            grpNew.Controls.Add(macCaption);

            _txtMac = new TextBox();
            _txtMac.SetBounds(14, 47, 172, 21);
            _txtMac.CharacterCasing = CharacterCasing.Upper;
            _txtMac.MaxLength = 17;
            _txtMac.Font = new Font("Consolas", 9.75f);
            _txtMac.TextChanged += OnMacTextChanged;
            grpNew.Controls.Add(_txtMac);

            _lblMacState = NewLabel("");
            _lblMacState.SetBounds(14, 71, 270, 15);
            _lblMacState.ForeColor = Color.DarkSlateBlue;
            _lblMacState.Font = new Font(Font.FontFamily, 7.6f);
            grpNew.Controls.Add(_lblMacState);

            _btnRandom = new Button();
            _btnRandom.Text = "Random";
            _btnRandom.SetBounds(193, 46, 86, 23);
            _btnRandom.Click += OnRandomClicked;
            grpNew.Controls.Add(_btnRandom);

            Label vendorCaption = NewLabel("Vendor:");
            vendorCaption.SetBounds(291, 30, 60, 16);
            grpNew.Controls.Add(vendorCaption);

            _txtVendorSearch = new TextBox();
            _txtVendorSearch.Location = new Point(291, 46);
            _txtVendorSearch.Size = new Size(140, 21);
            _txtVendorSearch.TextChanged += delegate { ShopVendors(_txtVendorSearch.Text); };
            grpNew.Controls.Add(_txtVendorSearch);

            _cmbVendor = new ComboBox();
            _cmbVendor.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbVendor.IntegralHeight = false;
            _cmbVendor.DropDownHeight = 200;
            _cmbVendor.Location = new Point(291, 70);
            _cmbVendor.Size = new Size(300, 21);
            _cmbVendor.SelectedIndexChanged += OnVendorSelected;
            grpNew.Controls.Add(_cmbVendor);

            _lblVendorCount = NewLabel("");
            _lblVendorCount.SetBounds(291, 94, 300, 14);
            _lblVendorCount.ForeColor = Color.DimGray;
            _lblVendorCount.Font = new Font(Font.FontFamily, 7.6f);
            grpNew.Controls.Add(_lblVendorCount);

            _chkAuto02 = new CheckBox();
            _chkAuto02.Text = "Use '02' as first octet of MAC address (workaround for some Wi-Fi adapters)";
            _chkAuto02.SetBounds(14, 102, 510, 18);
            grpNew.Controls.Add(_chkAuto02);

            GroupBox grpPersist = NewGroup("Persistence", 14, 126, 496, 84);
            _rbPersistent = new RadioButton();
            _rbPersistent.Text = "Persistent (survives reboot)";
            _rbPersistent.SetBounds(10, 20, 220, 18);
            _rbPersistent.Checked = true;
            grpPersist.Controls.Add(_rbPersistent);
            _rbTemp = new RadioButton();
            _rbTemp.Text = "Non-persistent (reverts on reboot)";
            _rbTemp.SetBounds(240, 20, 240, 18);
            grpPersist.Controls.Add(_rbTemp);
            _chkRestartNow = new CheckBox();
            _chkRestartNow.Text = "Restart adapter now (uncheck: value just sits in the registry," +
                                  " applies at the next disable/enable or reboot)";
            _chkRestartNow.SetBounds(10, 44, 474, 30);
            _chkRestartNow.Checked = true;
            _chkRestartNow.CheckedChanged += delegate
            {
                _rbTemp.Enabled = _chkRestartNow.Checked;
                if (!_chkRestartNow.Checked) _rbPersistent.Checked = true;
            };
            ToolTip tt2 = new ToolTip();
            tt2.SetToolTip(_chkRestartNow,
                "MAC spoofing takes effect when a driver (re-)initializes.\n" +
                "Checked: the adapter is disabled and re-enabled right now.\n" +
                "Unchecked: only the registry value is written; Windows applies it\n" +
                "the next time the adapter restarts for any reason.\n" +
                "Note: non-persistent mode requires an immediate restart.");
            grpPersist.Controls.Add(_chkRestartNow);
            grpNew.Controls.Add(grpPersist);

            ToolTip tt = new ToolTip();
            tt.SetToolTip(_chkAuto02,
                "Some Wi-Fi drivers only accept locally administered addresses.\n" +
                "Checked: generated MACs start with 02; typed MACs get their first\n" +
                "octet replaced by 02 before being written.");

            Controls.Add(grpNew);

            _btnApply = new Button();
            _btnApply.Text = "Change MAC now";
            _btnApply.SetBounds(12, 524, 200, 30);
            _btnApply.Font = new Font(Font.FontFamily, 9f, FontStyle.Bold);
            _btnApply.Click += OnApplyClicked;
            Controls.Add(_btnApply);

            _btnRestore = new Button();
            _btnRestore.Text = "Restore Original";
            _btnRestore.SetBounds(224, 524, 200, 30);
            _btnRestore.Click += OnRestoreClicked;
            Controls.Add(_btnRestore);

            _btnHelp = new Button();
            _btnHelp.Text = "Help";
            _btnHelp.SetBounds(440, 524, 92, 30);
            _btnHelp.Click += OnHelpClicked;
            Controls.Add(_btnHelp);

            _batchMenu = new ContextMenuStrip();
            _batchMenu.Items.Add("Randomize all up adapters", null, delegate { BatchRandomize(); });
            _batchMenu.Items.Add("Restore all changed adapters", null, delegate { BatchRestore(); });

            _btnBatch = new Button();
            _btnBatch.Text = "Batch...";
            _btnBatch.SetBounds(532, 524, 104, 30);
            _btnBatch.Click += OnBatchClicked;
            Controls.Add(_btnBatch);

            ToolTip ttBatch = new ToolTip();
            ttBatch.SetToolTip(_btnBatch, "Apply a change to several adapters at once.");

            GroupBox grpLog = NewGroup("Log", 12, 560, 624, 132);
            _txtLog = new TextBox();
            _txtLog.SetBounds(12, 20, 594, 102);
            _txtLog.Multiline = true;
            _txtLog.ReadOnly = true;
            _txtLog.ScrollBars = ScrollBars.Vertical;
            _txtLog.WordWrap = false;
            _txtLog.Font = new Font("Consolas", 8.4f);
            grpLog.Controls.Add(_txtLog);
            Controls.Add(grpLog);

            _lblSt = NewLabel("Starting...");
            _lblSt.SetBounds(12, 700, 330, 16);
            Controls.Add(_lblSt);

            _lblDbInfo = NewLabel("");
            _lblDbInfo.SetBounds(350, 700, 286, 16);
            _lblDbInfo.TextAlign = ContentAlignment.MiddleRight;
            _lblDbInfo.ForeColor = Color.DimGray;
            Controls.Add(_lblDbInfo);
        }

        private void AddInfoRow(Label value, string caption, int row)
        {
            _infoTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 21));
            value.Margin = new Padding(0);
            value.Dock = DockStyle.Fill;
            value.TextAlign = ContentAlignment.MiddleLeft;
            _infoTable.Controls.Add(value, 1, row);

            Label c = NewLabel(caption);
            c.ForeColor = Color.DimGray;
            c.Margin = new Padding(0);
            c.Dock = DockStyle.Fill;
            c.TextAlign = ContentAlignment.MiddleLeft;
            _infoTable.Controls.Add(c, 0, row);
        }

        private void ShopVendors(string filter)
        {
            int total = 0;
            IList<VendorDb.VendorItem> matches = VendorDb.Search(filter, MaxVendorsShown, out total);

            VendorDb.VendorItem selected = SelectedVendorItem();
            string keep = (selected != null) ? selected.Name : null;

            _suppressVendorSel = true;
            _cmbVendor.BeginUpdate();
            _cmbVendor.Items.Clear();
            foreach (VendorDb.VendorItem item in matches) _cmbVendor.Items.Add(item);
            _cmbVendor.EndUpdate();

            int index = 0;
            for (int i = 1; i < matches.Count; i++)
            {
                if (keep != null && matches[i].Name == keep) { index = i; break; }
            }
            _cmbVendor.SelectedIndex = index;
            _suppressVendorSel = false;

            if (filter.Trim().Length > 0)
            {
                _lblVendorCount.Text = total + " vendor(s) match '" + filter.Trim() + "'";
                if (total > MaxVendorsShown) _lblVendorCount.Text += ", showing first " + MaxVendorsShown;
            }
            else
            {
                _lblVendorCount.Text = total + " vendors - type above to search";
            }
        }

        private void UpdateDbLabel()
        {
            _lblDbInfo.Text = "Vendor database: " + VendorDb.EntryCount.ToString("N0") +
                              " OUIs (" + VendorDb.SourceDescription + ")";
        }

        private VendorDb.VendorItem SelectedVendorItem()
        {
            return _cmbVendor.SelectedItem as VendorDb.VendorItem;
        }

        private void OnVendorSelected(object sender, EventArgs e)
        {
            if (_suppressVendorSel) return;
            VendorDb.VendorItem item = SelectedVendorItem();
            if (item == null) return;

            string prefix = item.Prefix;
            bool force02 = _chkAuto02.Checked;
            if (prefix != null && force02) prefix = "02" + prefix.Substring(2);

            string hex = MacUtil.GenerateRandom(prefix, force02);
            SetMacContents(hex);
        }

        private void OnRandomClicked(object sender, EventArgs e)
        {
            VendorDb.VendorItem item = SelectedVendorItem();
            string prefix = (item != null) ? item.Prefix : null;
            string hex = MacUtil.GenerateRandom(prefix, _chkAuto02.Checked);
            SetMacContents(hex);
        }

        private void SetMacContents(string hex12)
        {
            _txtMac.TextChanged -= OnMacTextChanged;
            _txtMac.Text = MacUtil.Format(hex12);
            _txtMac.TextChanged += OnMacTextChanged;
            OnMacTextChanged(null, EventArgs.Empty);
        }

        private void OnMacTextChanged(object sender, EventArgs e)
        {
            string hex = MacUtil.Clean(_txtMac.Text);
            if (_txtMac.Text.Trim().Length == 0)
            {
                _lblMacState.Text = "";
            }
            else if (hex == null)
            {
                _lblMacState.Text = "(incomplete or invalid - needs 12 hex digits)";
            }
            else
            {
                string vendor = VendorDb.Identify(hex);
                _lblMacState.Text = (vendor == null) ? "Vendor: unknown" : "Vendor: " + vendor;
            }
            UpdateButtons();
        }

        private string EffectiveMac(string hex12)
        {
            if (hex12 == null) return null;
            if (_chkAuto02.Checked) return "02" + hex12.Substring(2);
            return hex12;
        }

        private AdapterInfo SelectedAdapter()
        {
            return _cmbAdapter.SelectedItem as AdapterInfo;
        }

        private async void RefreshAdaptersAsync()
        {
            if (_busy) return;
            _busy = true;
            _btnRefresh.Enabled = false;
            _chkVirtual.Enabled = false;
            _chkUpOnly.Enabled = false;
            _txtAdapterFilter.Enabled = false;
            _btnBatch.Enabled = false;
            _lblSt.Text = "Scanning network adapters (this takes a few seconds) ...";

            bool includeVirtual = _chkVirtual.Checked;
            bool upOnly = _chkUpOnly.Checked;
            List<AdapterInfo> list = null;
            string error = null;
            try
            {
                list = await Task.Run(() => AdapterService.EnumerateAdapters(includeVirtual, upOnly));
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            _busy = false;
            _chkVirtual.Enabled = true;
            _chkUpOnly.Enabled = true;
            _txtAdapterFilter.Enabled = true;
            _btnBatch.Enabled = true;

            if (error != null)
            {
                Log("ERROR: adapter scan failed: " + error);
                _lblSt.Text = "Adapter scan failed.";
                MessageBox.Show(this, error, "Adapter scan failed",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateButtons();
                return;
            }

            _adapters = list;
            string previousGuid = null;
            AdapterInfo previously = SelectedAdapter();
            if (previously != null) previousGuid = previously.NetCfgInstanceId;

            PopulateAdapterList(previousGuid);
            if (_adapters.Count == 0)
            {
                UpdateAdapterPanel(null);
                Log(upOnly
                    ? "No up adapters were found - clear 'Only show up adapters' to see all of them."
                    : "No network adapters were found.");
            }
            else if (_cmbAdapter.Items.Count == 0)
            {
                UpdateAdapterPanel(null);
                Log("No adapter matches '" + _txtAdapterFilter.Text.Trim() + "' - clear the filter box.");
            }
            else if (previousGuid == null && _txtMac.Text.Trim().Length == 0)
            {
                SetMacContents(MacUtil.GenerateRandom(null, _chkAuto02.Checked));
            }
            _lblSt.Text = _adapters.Count + " adapter(s) found. Ready.";
            UpdateButtons();
        }

        private void PopulateAdapterList(string keepGuid)
        {
            // rebuilds the dropdown from the cached list, selection kept by GUID
            string filter = _txtAdapterFilter.Text;
            List<AdapterInfo> shown = new List<AdapterInfo>();
            foreach (AdapterInfo a in _adapters)
            {
                if (a.Matches(filter)) shown.Add(a);
            }

            _cmbAdapter.BeginUpdate();
            _cmbAdapter.Items.Clear();
            foreach (AdapterInfo a in shown) _cmbAdapter.Items.Add(a);
            _cmbAdapter.EndUpdate();

            int select = -1;
            int fallback = -1;
            for (int i = 0; i < shown.Count; i++)
            {
                AdapterInfo a = shown[i];
                if (select < 0 && keepGuid != null && a.NetCfgInstanceId == keepGuid) select = i;
                if (fallback < 0 && a.IsUp() && !string.IsNullOrEmpty(a.CurrentMac)) fallback = i;
            }
            if (select < 0) select = (fallback >= 0) ? fallback : (shown.Count > 0 ? 0 : -1);
            _cmbAdapter.SelectedIndex = select;

            _lblAdapterCount.Text = (shown.Count == _adapters.Count)
                ? _adapters.Count + " adapter(s)"
                : shown.Count + " of " + _adapters.Count + " shown";
            if (shown.Count == 0) UpdateAdapterPanel(null);
        }

        private void OnAdapterFilterChanged(object sender, EventArgs e)
        {
            if (_busy) return;
            AdapterInfo current = SelectedAdapter();
            PopulateAdapterList(current != null ? current.NetCfgInstanceId : null);
            UpdateButtons();
        }

        private void OnAdapterSelected(object sender, EventArgs e)
        {
            UpdateAdapterPanel(SelectedAdapter());
        }

        private void UpdateAdapterPanel(AdapterInfo a)
        {
            if (a == null)
            {
                _lblInfoHeader.Text = "(no adapter selected)";
                _lblDescription.Text = ""; _lblConnection.Text = ""; _lblType.Text = "";
                _lblState.Text = ""; _lblCurrentMac.Text = ""; _lblOriginalMac.Text = "";
                _lblOverride.Text = ""; _lblCurrentMac.ForeColor = Color.Black;
                UpdateButtons();
                return;
            }

            string subkey = a.RegistryKeyPath().Replace(AdapterService.BaseKey + "\\", "");
            _lblInfoHeader.Text = "[" + subkey + "]  " +
                                  (a.IsChangeable() ? "" : "not changeable (no registry entry)");
            _lblDescription.Text = a.Description;
            _lblConnection.Text = string.IsNullOrEmpty(a.ConnectionName) ? "(none)" : a.ConnectionName;
            _lblType.Text = (a.IsVirtual ? "virtual - " : "") + (a.IsWireless ? "wireless (Wi-Fi)" : "wired / other");
            _lblState.Text = a.StateText();

            _lblCurrentMac.Text = (a.CurrentMac == null)
                ? "(not available - adapter disabled)"
                : MacUtil.Format(a.CurrentMac);
            _lblOriginalMac.Text = (a.OriginalMac == null)
                ? "(not recorded yet)"
                : MacUtil.Format(a.OriginalMac);

            if (a.RegistryMac != null)
            {
                _lblOverride.Text = MacUtil.Format(a.RegistryMac) + "   [active software override]";
                _lblOverride.ForeColor = Color.Firebrick;
                _lblCurrentMac.ForeColor = Color.Firebrick;
            }
            else
            {
                _lblOverride.Text = "none (using factory address)";
                _lblOverride.ForeColor = Color.Black;
                _lblCurrentMac.ForeColor = Color.Black;
            }
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            if (_busy)
            {
                _btnRefresh.Enabled = false;
                _btnApply.Enabled = false;
                _btnRestore.Enabled = false;
                _btnRandom.Enabled = false;
                _cmbVendor.Enabled = false;
                _chkAuto02.Enabled = false;
                _cmbAdapter.Enabled = false;
                _txtVendorSearch.Enabled = false;
                _txtAdapterFilter.Enabled = false;
                _btnBatch.Enabled = false;
                return;
            }
            AdapterInfo a = SelectedAdapter();
            _btnRefresh.Enabled = true;
            _btnRandom.Enabled = true;
            _cmbVendor.Enabled = true;
            _chkAuto02.Enabled = true;
            _cmbAdapter.Enabled = true;
            _txtVendorSearch.Enabled = true;
            _txtAdapterFilter.Enabled = true;
            _btnBatch.Enabled = (_adapters.Count > 0);

            string hex = MacUtil.Clean(_txtMac.Text);
            _btnApply.Enabled = (a != null && a.IsChangeable() && hex != null);
            _btnRestore.Enabled = (a != null && NeedsRestore(a));
        }

        private void OnBatchClicked(object sender, EventArgs e)
        {
            if (_busy || _adapters.Count == 0) return;
            _batchMenu.Show(_btnBatch, new Point(0, _btnBatch.Height));
        }

        private static bool NeedsRestore(AdapterInfo a)
        {
            if (a == null || !a.IsChangeable()) return false;
            if (a.RegistryMac != null) return true;
            return a.OriginalMac != null && a.CurrentMac != null &&
                   StringComparer.OrdinalIgnoreCase.Compare(a.OriginalMac, a.CurrentMac) != 0;
        }

        private static string AdapterName(AdapterInfo a)
        {
            return string.IsNullOrEmpty(a.ConnectionName) ? a.Description : a.ConnectionName;
        }

        private static string TargetList(List<AdapterInfo> list)
        {
            string s = "";
            for (int i = 0; i < list.Count && i < 8; i++) s += "  - " + AdapterName(list[i]) + "\r\n";
            if (list.Count > 8) s += "  ... and " + (list.Count - 8) + " more\r\n";
            return s;
        }

        private void BatchRandomize()
        {
            if (_busy) return;
            // only adapters Windows calls up and that have a registry subkey
            List<AdapterInfo> targets = _adapters.FindAll(delegate(AdapterInfo a)
            {
                return a.IsUp() && a.IsChangeable();
            });
            if (targets.Count == 0)
            {
                MessageBox.Show(this, "No up, usable adapter in the list.\n" +
                                "Turn on 'Include hidden/virtual adapters' to widen the search.",
                                "Randomize all up adapters", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool restartNow = _chkRestartNow.Checked;
            bool persistent = _rbPersistent.Checked || !restartNow;
            string prompt = "Give " + targets.Count + " adapter(s) a new random MAC?\r\n\r\n" +
                            TargetList(targets) +
                            "\r\nMode: " + (persistent ? "persistent" : "non-persistent") +
                            (restartNow ? ", each adapter restarted in turn." : ", registry only.") +
                            "\r\n\r\nContinue?";
            if (MessageBox.Show(this, prompt, "Randomize all up adapters",
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            VendorDb.VendorItem vendor = SelectedVendorItem();
            string prefix = (vendor != null) ? vendor.Prefix : null;
            bool force02 = _chkAuto02.Checked;

            Log("---------------------------------------------------------------------");
            Log("Batch: randomizing " + targets.Count + " up adapter(s).");
            RunOperation(delegate
            {
                int changed = 0, rejected = 0, failed = 0;
                foreach (AdapterInfo a in targets)
                {
                    string hex = MacUtil.GenerateRandom(prefix, force02);
                    Log(AdapterName(a) + " -> " + MacUtil.Format(hex));
                    try
                    {
                        AdapterService.OperationResult r = AdapterService.ApplyMac(a, hex, persistent, restartNow, Log);
                        if (r.Message != null && !r.Success)
                        {
                            failed++;
                            Log("  " + r.Message);
                        }
                        else if (r.DriverRejected)
                        {
                            rejected++;
                            Log("  driver kept " + MacUtil.Format(r.Adapter.CurrentMac));
                        }
                        else changed++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Log("  ERROR: " + ex.Message);
                    }
                }
                return "Batch done: " + changed + " changed, " + rejected +
                       " rejected by the driver, " + failed + " failed.";
            });
        }

        private void BatchRestore()
        {
            if (_busy) return;
            List<AdapterInfo> targets = _adapters.FindAll(NeedsRestore);
            if (targets.Count == 0)
            {
                MessageBox.Show(this, "No adapter in the list carries a software override.",
                                "Restore all changed adapters", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool restartNow = _chkRestartNow.Checked;
            string prompt = "Restore the factory MAC of " + targets.Count + " adapter(s)?\r\n\r\n" +
                            TargetList(targets) + "\r\n" +
                            (restartNow
                                ? "Each adapter is restarted after the override is removed."
                                : "The factory address returns at the next adapter restart.") +
                            "\r\n\r\nContinue?";
            if (MessageBox.Show(this, prompt, "Restore all changed adapters",
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            Log("---------------------------------------------------------------------");
            Log("Batch: restoring " + targets.Count + " adapter(s).");
            RunOperation(delegate
            {
                int restored = 0, failed = 0;
                foreach (AdapterInfo a in targets)
                {
                    Log(AdapterName(a) + " -> factory address");
                    try
                    {
                        AdapterService.OperationResult r = AdapterService.RestoreMac(a, restartNow, Log);
                        if (r.Message != null && !r.Success)
                        {
                            failed++;
                            Log("  " + r.Message);
                        }
                        else restored++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Log("  ERROR: " + ex.Message);
                    }
                }
                return "Batch done: " + restored + " restored, " + failed + " failed.";
            });
        }

        private void OnApplyClicked(object sender, EventArgs e)
        {
            AdapterInfo a = SelectedAdapter();
            if (a == null) return;

            string hex = MacUtil.Clean(_txtMac.Text);
            if (hex == null)
            {
                MessageBox.Show(this, "Please enter a valid MAC address (12 hex digits).",
                                "Invalid MAC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            hex = EffectiveMac(hex);
            bool restartNow = _chkRestartNow.Checked;
            bool persistent = _rbPersistent.Checked || !restartNow;

            string prompt;
            if (restartNow)
            {
                prompt =
                    "Adapter:   " + a.ToString() + "\n" +
                    "New MAC:   " + MacUtil.Format(hex) + "\n" +
                    "Mode:      " + (persistent
                         ? "persistent (survives reboot)"
                         : "non-persistent (reverts on reboot)") +
                    "\n\nThe adapter will be disabled and re-enabled; connections\n" +
                    "running through it will drop for a few seconds.\n\nContinue?";
            }
            else
            {
                prompt =
                    "Adapter:   " + a.ToString() + "\n" +
                    "New MAC:   " + MacUtil.Format(hex) + "\n" +
                    "Mode:      registry only (persistent)\n\n" +
                    "No adapter restart will be performed. The address applies the\n" +
                    "next time the adapter is disabled/enabled, its driver reloads\n" +
                    "or the computer restarts.\n\nWrite the registry value?";
            }
            DialogResult dr = MessageBox.Show(this, prompt, "Change MAC address",
                                              MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dr != DialogResult.Yes) return;

            Log("---------------------------------------------------------------------");
            Log("Changing '" + a.ConnectionName + "' (" + a.Description + ") to " + MacUtil.Format(hex));
            Log(restartNow
                    ? (persistent ? "Mode: persistent, restarting adapter now." : "Mode: non-persistent, restarting adapter now.")
                    : "Mode: registry only (applies at next adapter restart).");

            string target = hex;
            RunOperation(delegate
            {
                AdapterService.OperationResult r =
                    AdapterService.ApplyMac(a, target, persistent, restartNow, Log);
                if (r.Message != null) return r.Message;
                if (r.Verified)
                    return "Success. Adapter reports the new MAC: " + MacUtil.Format(r.Adapter.CurrentMac);
                return "Done, but the adapter still reports " + MacUtil.Format(r.Adapter.CurrentMac) +
                       " - the driver may not accept the address (try the '02' octet option).";
            });
        }

        private void OnRestoreClicked(object sender, EventArgs e)
        {
            AdapterInfo a = SelectedAdapter();
            if (a == null) return;

            string detail = _chkRestartNow.Checked
                ? "Any software override in the registry will be removed\nand the adapter will be restarted."
                : "Any software override in the registry will be removed.\n" +
                  "The factory address returns at the next adapter restart.";
            DialogResult dr = MessageBox.Show(this,
                "Restore the factory (burnt-in) MAC address of\n'" +
                ((a.ConnectionName != null) ? a.ConnectionName : a.Description) + "'?\n\n" + detail,
                "Restore original MAC", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dr != DialogResult.Yes) return;

            Log("---------------------------------------------------------------------");
            Log("Restoring factory address of '" + a.ConnectionName + "'.");
            bool restartNow = _chkRestartNow.Checked;
            RunOperation(delegate
            {
                AdapterService.OperationResult r = AdapterService.RestoreMac(a, restartNow, Log);
                if (r.Message != null) return r.Message;
                return "Restore complete. Current MAC: " + MacUtil.Format(r.Adapter.CurrentMac);
            });
        }

        private async void RunOperation(Func<string> operation)
        {
            if (_busy) return;
            _busy = true;
            UseWaitCursor = true;
            UpdateButtons();
            _lblSt.Text = "Working - please wait ...";

            string ok = null;
            string error = null;
            try
            {
                ok = await Task.Run(() => operation());
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            _busy = false;
            UseWaitCursor = false;

            List<AdapterInfo> list;
            try
            {
                list = await Task.Run(() => AdapterService.EnumerateAdapters(
                    _chkVirtual.Checked, _chkUpOnly.Checked));
                _adapters = list;
                string guid = null;
                AdapterInfo current = SelectedAdapter();
                if (current != null) guid = current.NetCfgInstanceId;
                PopulateAdapterList(guid);
            }
            catch (Exception)
            {
                UpdateAdapterPanel(SelectedAdapter());
            }

            _lblSt.Text = ok ?? ("Failed: " + error);
            if (ok != null) Log(ok);
            if (error != null)
            {
                Log("ERROR: " + error);
                MessageBox.Show(this, error, "Operation failed",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnHelpClicked(object sender, EventArgs e)
        {
            MessageBox.Show(this,
                "MAC Address Changer (freeware)\n" +
                "--------------------------------\n\n" +
                "How it works:\n" +
                "- Change MAC writes a NetworkAddress value into the adapter's registry\n" +
                "  subkey (HKLM\\SYSTEM\\CurrentControlSet\\Control\\Class\\{4D36E972-...}\n" +
                "  \\00NN). Adapters are matched to their subkey through NetCfgInstanceId\n" +
                "  (Win32_NetworkAdapterConfiguration.SettingID, joined by Index), with\n" +
                "  MatchingDeviceId as a fallback.\n\n" +
                "- 'Restart adapter now' cycles the adapter immediately so the driver\n" +
                "  picks the value up. If unchecked, only the registry value is written:\n" +
                "  Windows applies it the next time the adapter restarts for any reason.\n" +
                "  (Non-persistent mode always requires an immediate restart.)\n\n" +
                "- Persistent keeps that value in the registry, so the MAC survives\n" +
                "  reboots until you remove it. Non-persistent deletes the value right\n" +
                "  after the adapter comes back up: the new MAC stays active until the\n" +
                "  next reboot, adapter restart or driver reload.\n\n" +
                "- Restore Original removes the override so it uses the burnt-in\n" +
                "  factory address again.\n\n" +
                "- Random fills the field with a random vendor prefix and a random\n" +
                "  tail. Type in the vendor search box to narrow the list, or pick a\n" +
                "  vendor to stay within its OUI range.\n\n" +
                "- 'Include hidden/virtual adapters' shows every adapter Windows\n" +
                "  knows about, including emulated ones (WAN Miniports, Wi-Fi Direct,\n" +
                "  TAP, vEthernet, KM-TEST loopback, VPN taps) and disabled or detached\n" +
                "  virtual devices, listed under their Windows name such as\n" +
                "  'Local Area Connection* 1'. Pseudo interfaces without a MAC\n" +
                "  (Teredo/ISATAP/6to4/IP-HTTPS) stay hidden.\n\n" +
                "- 'Only show up adapters' lists just the adapters Windows reports as\n" +
                "  up; disabled, disconnected and not-present ones are hidden. It\n" +
                "  combines with the virtual toggle.\n\n" +
                "- The filter box narrows the list as you type: connection names,\n" +
                "  descriptions, MAC fragments, 'up', 'virtual' and 'wireless'\n" +
                "  all match.\n\n" +
                "- 'Batch...' changes several adapters at once. 'Randomize all up\n" +
                "  adapters' and 'Restore all changed adapters' act on the adapters\n" +
                "  currently listed, using the persistence and restart settings\n" +
                "  below. Failures on one adapter do not stop the rest.\n\n" +
                "- Some Wi-Fi drivers only accept locally administered addresses. If a\n" +
                "  change does not take effect, enable \"Use '02' as first octet\" and\n" +
                "  try again.\n\n" +
                "All original MAC addresses are remembered in\n" +
                "%APPDATA%\\MacChanger, so Restore Original works across sessions.\n\n" +
                "Use responsibly: spoofing MAC addresses may violate laws or the rules\n" +
                "of networks you connect to.",
                "About / Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Log(string line)
        {
            if (_txtLog.InvokeRequired)
            {
                BeginInvoke((Action)delegate { Log(line); });
                return;
            }
            _txtLog.AppendText(string.Format("[{0:HH:mm:ss}]  {1}\r\n", DateTime.Now, line));
        }
    }
}
