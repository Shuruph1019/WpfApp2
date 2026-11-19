using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WpfApp2;

namespace WpfApp2
{
    public partial class MainWindow : Window
    {
        User SelUser;
        List<CheckBox> CatChecks = new List<CheckBox>();

        static string BackupDir
        {
            get { return System.IO.Path.Combine(Db.Dir, "Backups"); }
        }
        public MainWindow()
        {
            InitializeComponent();
            Title = "Учёт ТМЦ — " + App.CurrentUser.FullName;

            if (App.CurrentUser.IsAdmin)
            {
                TabUsers.Visibility = Visibility.Visible;
                TabBackup.Visibility = Visibility.Visible;
                PnlNewCat.Visibility = Visibility.Visible;
            }

            TbDbPath.Text = "Файл данных: " + Db.FilePath;
            LoadCategories();
            LoadUsers();
            LoadBackups();
            RefreshInHistory();
            RefreshOutHistory();
            RefreshBalances();
            RefreshOrders();
            RefreshReport();
        }

        string ItemName(int id)
        {
            var i = Db.FindItem(id);
            return i == null ? "(удалён)" : i.Name;
        }

        string ItemUnit(int id)
        {
            var i = Db.FindItem(id);
            return i == null ? "" : i.Unit;
        }

        double Balance(int itemId)
        {
            double sum = 0;
            foreach (var m in Db.Store.Moves)
                if (m.ItemId == itemId) sum += m.Type == "in" ? m.Qty : -m.Qty;
            return sum;
        }

        HashSet<int> AllowedCats()
        {
            var res = new HashSet<int>();
            if (App.CurrentUser.IsAdmin)
            {
                foreach (var c in Db.Store.Categories) res.Add(c.Id);
                return res;
            }
            foreach (var x in Db.Store.UserCars)
                if (x.UserId == App.CurrentUser.Id) res.Add(x.CategoryId);
            return res;
        }

        void LoadCategories()
        {
            var cats = Db.Store.Categories.OrderBy(c => c.Name).ToList();

            CbInCategory.ItemsSource = cats;
            CbInCategory.DisplayMemberPath = "Name";
            CbInCategory.SelectedValuePath = "Id";
            if (CbInCategory.SelectedIndex < 0 && cats.Count > 0) CbInCategory.SelectedIndex = 0;

            var allowedCats = cats.Where(c => AllowedCats().Contains(c.Id)).ToList();
            CbOutCategory.ItemsSource = allowedCats;
            CbOutCategory.DisplayMemberPath = "Name";
            CbOutCategory.SelectedValuePath = "Id";
            if (CbOutCategory.SelectedIndex < 0 && allowedCats.Count > 0) CbOutCategory.SelectedIndex = 0;

            LoadItems();
        }

        int? SelCar(ComboBox cb)
        {
            if (cb.SelectedValue == null) return null;
            return Convert.ToInt32(cb.SelectedValue);
        }

        void LoadItems()
        {
            var c1 = SelCar(CbInCategory);
            CbInItem.ItemsSource = c1 == null
                ? null
                : Db.Store.Items.Where(i => i.CategoryId == c1.Value).OrderBy(i => i.Name).ToList();
            CbInItem.DisplayMemberPath = "Name";
            CbInItem.SelectedValuePath = "Id";

            var c2 = SelCar(CbOutCategory);
            CbOutItem.ItemsSource = c2 == null
                ? null
                : Db.Store.Items.Where(i => i.CategoryId == c2.Value).OrderBy(i => i.Name).ToList();
            CbOutItem.DisplayMemberPath = "Name";
            CbOutItem.SelectedValuePath = "Id";
        }

        void CbInCategory_Changed(object s, SelectionChangedEventArgs e) { LoadItems(); }
        void CbOutCategory_Changed(object s, SelectionChangedEventArgs e) { LoadItems(); }

        void BtnNewCat_Click(object sender, RoutedEventArgs e)
        {
            var name = TbNewCat.Text.Trim();
            if (name.Length == 0) return;
            bool exists = false;
            foreach (var c in Db.Store.Categories)
                if (string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)) { exists = true; break; }
            if (exists) { MessageBox.Show("Такая категория уже существует."); return; }

            Db.Store.Categories.Add(new Category { Id = Db.NextId(Db.Store.Categories), Name = name });
            Db.Save();
            TbNewCat.Clear();
            LoadCategories();
            RefreshBalances();
        }

        void BtnNewItem_Click(object sender, RoutedEventArgs e)
        {
            var cat = SelCar(CbInCategory);
            var name = TbNewName.Text.Trim();
            if (cat == null || name.Length == 0)
            { MessageBox.Show("Выберите категорию и укажите название товара."); return; }

            var it = new Item();
            it.Id = Db.NextId(Db.Store.Items);
            it.CategoryId = cat.Value;
            it.Name = name;
            it.Unit = string.IsNullOrWhiteSpace(TbNewUnit.Text) ? "шт" : TbNewUnit.Text.Trim();
            Db.Store.Items.Add(it);
            Db.Save();
            TbNewName.Clear();
            LoadItems();
        }

        void BtnInAdd_Click(object sender, RoutedEventArgs e)
        {
            var item = CbInItem.SelectedItem as Item;
            if (item == null) { MessageBox.Show("Выберите товар."); return; }
            double qty, price;
            if (!Db.TryDec(TbInQty.Text, out qty) || qty <= 0) { MessageBox.Show("Укажите количество."); return; }
            Db.TryDec(TbInPrice.Text, out price);

            var mv = new Move();
            mv.Id = Db.NextId(Db.Store.Moves);
            mv.ItemId = item.Id; mv.Type = "in"; mv.Qty = qty; mv.Price = price;
            mv.Dt = Db.Now(); mv.UserId = App.CurrentUser.Id;
            Db.Store.Moves.Add(mv);
            Db.Save();

            TbInQty.Clear(); TbInPrice.Clear(); TbInComment.Clear();
            RefreshInHistory(); RefreshBalances();
        }

        void RefreshInHistory()
        {
            DgInHistory.ItemsSource = Db.Store.Moves
                .Where(m => m.Type == "in").OrderByDescending(m => m.Id).Take(200)
                .Select(m => new
                {
                    Дата = m.Dt,
                    Товар = ItemName(m.ItemId),
                    Количество = m.Qty,
                    Цена = m.Price,
                    Комментарий = m.Comment
                }).ToList();
        }

        void BtnOutAdd_Click(object sender, RoutedEventArgs e)
        {
            var order = TbOrderNum.Text.Trim();
            var person = TbOutPerson.Text.Trim();
            if (order.Length == 0) { MessageBox.Show("Укажите номер заказа."); return; }
            if (person.Length == 0) { MessageBox.Show("Укажите, кому выдаётся товар."); return; }
            var item = CbOutItem.SelectedItem as Item;
            if (item == null) { MessageBox.Show("Выберите товар."); return; }
            double qty;
            if (!Db.TryDec(TbOutQty.Text, out qty) || qty <= 0) { MessageBox.Show("Укажите количество."); return; }

            var bal = Balance(item.Id);
            if (qty > bal)
            { MessageBox.Show("Недостаточно товара на складе. Остаток: " + bal); return; }

            var mv = new Move();
            mv.Id = Db.NextId(Db.Store.Moves);
            mv.ItemId = item.Id; mv.Type = "out"; mv.Qty = qty;
            mv.Person = person; mv.OrderNum = order;
            mv.Comment = TbOutComment.Text.Trim();
            mv.Dt = Db.Now(); mv.UserId = App.CurrentUser.Id;
            Db.Store.Moves.Add(mv);
            Db.Save();

            TbOutQty.Clear(); TbOutComment.Clear();
            RefreshOutHistory(); RefreshBalances(); RefreshOrders();
            MessageBox.Show("Товар выдан. Акт по заказу «" + order + "» сформирован (вкладка «Акты выдачи»).");
        }

        void RefreshOutHistory()
        {
            DgOutHistory.ItemsSource = Db.Store.Moves
                .Where(m => m.Type == "out").OrderByDescending(m => m.Id).Take(200)
                .Select(m => new
                {
                    Дата = m.Dt,
                    Заказ = m.OrderNum,
                    Товар = ItemName(m.ItemId),
                    Количество = m.Qty,
                    Получатель = m.Person
                }).ToList();
        }

        void RefreshBalances()
        {
            var f = TbSearch.Text.Trim().ToLower();
            DgBalances.ItemsSource = Db.Store.Items
                .Where(i => f.Length == 0 || i.Name.ToLower().Contains(f))
                .OrderBy(i => Db.Store.Categories.Where(c => c.Id == i.CategoryId)
                             .Select(c => c.Name).FirstOrDefault())
                .ThenBy(i => i.Name)
                .Select(i => new
                {
                    Товар = i.Name,
                    Категория = Db.Store.Categories.Where(c => c.Id == i.CategoryId)
                                 .Select(c => c.Name).FirstOrDefault(),
                    Единица = i.Unit,
                    Остаток = Balance(i.Id)
                }).ToList();
        }

        void TbSearch_Changed(object s, TextChangedEventArgs e) { RefreshBalances(); }
        void BtnBalRefresh_Click(object s, RoutedEventArgs e) { RefreshBalances(); }

        void BtnCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.Filter = "CSV (*.csv)|*.csv";
            dlg.FileName = "Остатки_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".csv";
            if (dlg.ShowDialog() != true) return;

            var sb = new StringBuilder("Товар;Категория;Единица;Остаток");
            foreach (dynamic row in DgBalances.Items)
            {
                sb.AppendLine();
                sb.Append(row.Товар).Append(';').Append(row.Категория).Append(';')
                  .Append(row.Единица).Append(';')
                  .Append(((double)row.Остаток).ToString(CultureInfo.InvariantCulture));
            }
            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show("Файл сохранён:\n" + dlg.FileName);
        }

        void RefreshOrders()
        {
            LbOrders.ItemsSource = Db.Store.Moves
                .Where(m => m.Type == "out" && m.OrderNum != "")
                .GroupBy(m => m.OrderNum)
                .Select(g => new OrderNow { Order = g.Key, LastDt = g.Min(x => x.Dt) })
                .OrderByDescending(r => r.LastDt)
                .ToList();
        }

        void LbOrders_Changed(object s, SelectionChangedEventArgs e)
        {
            var o = LbOrders.SelectedItem as OrderNow;
            TbAct.Text = o == null ? "" : BuildAct(o.Order);
        }

        string BuildAct(string order)
        {
            var rows = Db.Store.Moves
                .Where(m => m.Type == "out" && m.OrderNum == order)
                .OrderBy(m => m.Dt)
                .Select(m => new
                {
                    Dt = m.Dt,
                    Item = ItemName(m.ItemId),
                    Unit = ItemUnit(m.ItemId),
                    Qty = m.Qty,
                    Person = m.Person
                }).ToList();
            if (rows.Count == 0) return "";

            var sb = new StringBuilder();
            sb.AppendLine("АКТ ВЫДАЧИ ТМЦ № " + order);
            sb.AppendLine("от " + rows.Min(x => x.Dt));
            sb.AppendLine("Выдал: " + App.CurrentUser.FullName);
            sb.AppendLine("Получил: " + rows.Select(x => x.Person).Distinct().First());
            sb.AppendLine();
            sb.AppendLine(string.Format("{0,-4}{1,-44}{2,-6}{3,10}", "№", "Товар", "Ед.", "Кол-во"));
            sb.AppendLine(new string('-', 64));
            double total = 0; int n = 1;
            foreach (var m in rows)
            {
                sb.AppendLine(string.Format("{0,-4}{1,-44}{2,-6}{3,10}", n++, m.Item, m.Unit, m.Qty));
                total += m.Qty;
            }
            sb.AppendLine(new string('-', 64));
            sb.AppendLine("Итого позиций: " + rows.Count + ". Всего выдано: " + total);
            sb.AppendLine();
            sb.AppendLine("Выдал:  ____________ / _______________");
            sb.AppendLine("Получил: ____________ / _______________");
            return sb.ToString();
        }

        void BtnActSave_Click(object sender, RoutedEventArgs e)
        {
            var o = LbOrders.SelectedItem as OrderNow;
            if (o == null) { MessageBox.Show("Выберите заказ в списке."); return; }
            var dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.Filter = "Документ Word (*.docx)|*.docx";
            dlg.FileName = "Акт_" + o.Order + "_" + DateTime.Now.ToString("yyyyMMdd") + ".docx";
            if (dlg.ShowDialog() != true) return;

            try
            {
                var moves = Db.Store.Moves
                    .Where(m => m.Type == "out" && m.OrderNum == o.Order)
                    .OrderBy(m => m.Dt)
                    .ToList();

                var rows = moves.Select((m, i) => new ActRow
                {
                    No = i + 1,
                    Name = ItemName(m.ItemId),
                    Unit = ItemUnit(m.ItemId),
                    Qty = m.Qty
                }).ToList();

                var total = moves.Sum(m => m.Qty);
                var receiver = moves.Select(m => m.Person).FirstOrDefault(x => x != "") ?? "";
                var dtFrom = moves.Count > 0 ? moves.Min(m => m.Dt) : "";

                DocxSaver.SaveAct(dlg.FileName, o.Order, dtFrom,
                    App.CurrentUser.FullName, receiver, rows, total);

                MessageBox.Show("Акт сохранён:\n" + dlg.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось сохранить акт:\n" + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        void RefreshReport()
        {
            var typeSel = (CbMoveType.SelectedItem as ComboBoxItem) == null
                ? null : (CbMoveType.SelectedItem as ComboBoxItem).Content as string;
            IEnumerable<Move> q = Db.Store.Moves;
            if (typeSel == "Приход") q = q.Where(m => m.Type == "in");
            else if (typeSel == "Расход") q = q.Where(m => m.Type == "out");

            if (DpFrom.SelectedDate != null)
            {
                var s = DpFrom.SelectedDate.Value.ToString("yyyy-MM-dd") + " 00:00:00";
                q = q.Where(m => string.CompareOrdinal(m.Dt, s) >= 0);
            }
            if (DpTo.SelectedDate != null)
            {
                var s = DpTo.SelectedDate.Value.AddDays(1).ToString("yyyy-MM-dd") + " 00:00:00";
                q = q.Where(m => string.CompareOrdinal(m.Dt, s) < 0);
            }

            DgReport.ItemsSource = q.OrderByDescending(m => m.Id).Take(1000)
                .Select(m => new
                {
                    Дата = m.Dt,
                    Тип = m.Type == "in" ? "Приход" : "Расход",
                    Товар = ItemName(m.ItemId),
                    Единица = ItemUnit(m.ItemId),
                    Количество = m.Qty,
                    Цена = m.Price,
                    Получатель = m.Person,
                    Заказ = m.OrderNum,
                    Комментарий = m.Comment
                }).ToList();
        }

        void BtnRepRefresh_Click(object sender, RoutedEventArgs e) { RefreshReport(); }

        void LoadUsers()
        {
            LbUsers.ItemsSource = Db.Store.Users.OrderBy(u => u.Id).ToList();
            SelUser = null;
            SpUserCats.Children.Clear();
            CatChecks.Clear();
        }

        void LbUsers_Changed(object sender, SelectionChangedEventArgs e)
        {
            SelUser = LbUsers.SelectedItem as User;
            if (SelUser == null) return;

            TbULogin.Text = SelUser.Login;
            TbUName.Text = SelUser.FullName;
            PbUPass.Clear();
            ChkUAdmin.IsChecked = SelUser.IsAdmin;

            var allowed = new HashSet<int>();
            foreach (var x in Db.Store.UserCars)
                if (x.UserId == SelUser.Id) allowed.Add(x.CategoryId);

            SpUserCats.Children.Clear();
            CatChecks.Clear();
            foreach (var c in Db.Store.Categories.OrderBy(c => c.Name))
            {
                var cb = new CheckBox();
                cb.Content = c.Name;
                cb.Tag = c.Id;
                cb.IsChecked = allowed.Contains(c.Id);
                cb.Margin = new Thickness(0, 2, 0, 2);
                CatChecks.Add(cb);
                SpUserCats.Children.Add(cb);
            }
        }

        void SaveUserCats(int userId)
        {
            Db.Store.UserCars.RemoveAll(x => x.UserId == userId);
            foreach (var cb in CatChecks)
                if (cb.IsChecked == true)
                    Db.Store.UserCars.Add(new UserCar { UserId = userId, CategoryId = (int)cb.Tag });
        }

        void BtnUserSave_Click(object sender, RoutedEventArgs e)
        {
            if (SelUser == null) { MessageBox.Show("Выберите пользователя в списке."); return; }
            var login = TbULogin.Text.Trim();
            if (login.Length == 0) { MessageBox.Show("Укажите логин."); return; }
            if (Db.Store.Users.Any(u => u.Login == login && u.Id != SelUser.Id))
            { MessageBox.Show("Такой логин уже занят."); return; }

            SelUser.Login = login;
            SelUser.FullName = TbUName.Text.Trim();
            SelUser.IsAdmin = ChkUAdmin.IsChecked == true;
            if (PbUPass.Password.Length > 0)
                SelUser.PassHash = Db.Hash(PbUPass.Password);

            SaveUserCats(SelUser.Id);
            Db.Save();
            LoadCategories();
            LoadUsers();
            MessageBox.Show("Сохранено.");
        }

        void BtnUserAdd_Click(object sender, RoutedEventArgs e)
        {
            var login = TbULogin.Text.Trim();
            if (login.Length == 0 || PbUPass.Password.Length == 0)
            { MessageBox.Show("Для нового пользователя укажите логин и пароль."); return; }
            if (Db.Store.Users.Any(l => l.Login == login))
            { MessageBox.Show("Такой логин уже занят."); return; }

            var u = new User();
            u.Id = Db.NextId(Db.Store.Users);
            u.Login = login;
            u.PassHash = Db.Hash(PbUPass.Password);
            u.FullName = TbUName.Text.Trim();
            u.IsAdmin = ChkUAdmin.IsChecked == true;
            Db.Store.Users.Add(u);
            SaveUserCats(u.Id);
            Db.Save();
            LoadUsers();
            MessageBox.Show("Пользователь добавлен.");
        }

        void BtnUserDelete_Click(object sender, RoutedEventArgs e)
        {
            if (SelUser == null) { MessageBox.Show("Выберите пользователя."); return; }
            if (SelUser.Id == 1 || SelUser.Id == App.CurrentUser.Id)
            { MessageBox.Show("Этого пользователя удалить нельзя."); return; }

            if (MessageBox.Show("Удалить пользователя «" + SelUser.FullName + "»?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            Db.Store.UserCars.RemoveAll(x => x.UserId == SelUser.Id);
            Db.Store.Users.Remove(SelUser);
            Db.Save();
            LoadUsers();
        }

        void LoadBackups()
        {
            if (!Directory.Exists(BackupDir)) { LbBackups.ItemsSource = null; return; }
            LbBackups.ItemsSource = Directory.GetFiles(BackupDir, "*.xml")
                .Select(System.IO.Path.GetFileName)
                .OrderByDescending(x => x)
                .ToList();
        }

        void BtnBackupCreate_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(BackupDir);
            var file = System.IO.Path.Combine(BackupDir, "data_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".xml");
            File.Copy(Db.FilePath, file, true);
            LoadBackups();
            MessageBox.Show("Копия создана:\n" + file);
        }

        void BtnBackupRestore_Click(object sender, RoutedEventArgs e)
        {
            if (LbBackups.SelectedItem == null) { MessageBox.Show("Выберите копию в списке."); return; }
            if (MessageBox.Show("Восстановить данные из копии? Текущие будут заменены.\nПосле восстановления приложение закроется.",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            File.Copy(System.IO.Path.Combine(BackupDir, LbBackups.SelectedItem.ToString()), Db.FilePath, true);
            MessageBox.Show("Данные восстановлены. Запустите приложение заново.");
            Application.Current.Shutdown();
        }

        void Tabs_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            if (!ReferenceEquals(e.OriginalSource, Tabs)) return;


            RefreshInHistory();
            RefreshOutHistory();
            RefreshBalances();
            RefreshOrders();
            RefreshReport();
        }
    }
}