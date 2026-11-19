using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;
using System.Threading.Tasks;

namespace WpfApp2
{
    public interface IEntity { int Id { get; set; } }

    public class User : IEntity
    {
        public int Id { get; set; }
        public string Login { get; set; }
        public string PassHash { get; set; }
        public string FullName { get; set; }
        public bool IsAdmin { get; set; }
        public override string ToString()
        {
            return FullName + " (" + Login + ")" + (IsAdmin ? " - админ" : "");
        }
    }

    public class Category : IEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public override string ToString() { return Name; }
    }

    public class Item : IEntity
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string Name { get; set; }
        public string Unit { get; set; }
        public override string ToString() { return Name; }
    }

    public class Move : IEntity
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public string Type { get; set; }
        public double Qty { get; set; }
        public double Price { get; set; }
        public string Person { get; set; }
        public string OrderNum { get; set; }
        public string Comment { get; set; }
        public string Dt { get; set; }
        public int UserId { get; set; }

        public Move()
        {
            Type = "in"; Person = ""; OrderNum = ""; Comment = ""; Dt = "";
        }
    }

    public class UserCar
    {
        public int UserId { get; set; }
        public int CategoryId { get; set; }
    }

    public class Store
    {
        public List<User> Users { get; set; }
        public List<Category> Categories { get; set; }
        public List<Item> Items { get; set; }
        public List<Move> Moves { get; set; }
        public List<UserCar> UserCars { get; set; }

        public Store()
        {
            Users = new List<User>();
            Categories = new List<Category>();
            Items = new List<Item>();
            Moves = new List<Move>();
            UserCars = new List<UserCar>();
        }
    }

    public class OrderNow
    {
        public string Order { get; set; }
        public string LastDt { get; set; }
        public override string ToString()
        {
            return string.Format("Заказ {0}  (от {1})", Order, LastDt);
        }
    }

    public static class Db
    {
        public static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WpfApp8");
        public static readonly string FilePath = Path.Combine(Dir, "data.xml");
        public static readonly object Locker = new object();

        static Db()
        {
            Directory.CreateDirectory(Dir);
            Load();
            if (Store.Users.Count == 0)
            {
                var u = new User();
                u.Id = 1; u.Login = "admin"; u.PassHash = Hash("admin");
                u.FullName = "Администратор"; u.IsAdmin = true;
                Store.Users.Add(u);
            }
            if (Store.Categories.Count == 0)
                foreach (var n in new[] { "Инструменты", "Расходники", "Электрика", "СИЗ" })
                    Store.Categories.Add(new Category { Id = NextId(Store.Categories), Name = n });
            Save();
        }

        public static Store Store { get; private set; }

        static Store LoadFromDisk()
        {
            if (!File.Exists(FilePath)) return null;
            try
            {
                var ser = new XmlSerializer(typeof(Store));
                using (var fs = File.OpenRead(FilePath))
                    return (Store)ser.Deserialize(fs);
            }
            catch { return null; }
        }

        public static void Load()
        {
            var s = LoadFromDisk();
            Store = s ?? new Store();
        }

        public static void Save()
        {
            lock (Locker)
            {
                var ser = new XmlSerializer(typeof(Store));
                var tmp = FilePath + ".tmp";
                using (var fs = File.Create(tmp)) ser.Serialize(fs, Store);
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
        }

        public static int NextId(IEnumerable<IEntity> items)
        {
            return items.Any() ? items.Max(i => i.Id) + 1 : 1;
        }

        public static string Hash(string s)
        {
            return BitConverter.ToString((Encoding.UTF8.GetBytes(s))).Replace("-", "");
        }
        public static bool TryDec(string s, out double v)
        {
            if (s == null) s = "";
            return double.TryParse(s.Trim().Replace(',', '.'),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out v);
        }

        public static string Now()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public static Item FindItem(int id)
        {
            foreach (var i in Store.Items) if (i.Id == id) return i;
            return null;
        }
    }
}