using System;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Windows.Forms;
using ImageGlass.Common;
using System.Linq;

namespace ImageGlass {
    class AppConfig {
        public string IgVersion { get; set; }
        public Option<Rectangle> WindowsBound { get; set; }
        public FormWindowState State { get; set; }
        public bool IsShowCheckedBackground { get; set; }
        public bool IsShowToolBar { get; set; }
        public bool IsShowThumbnail { get; set; }

        public static AppConfig Load(Func<string, string, string> loader){
            Contract.Requires(loader != null);
            return new AppConfig{
                IgVersion = loader("igVersion", "0"),
                WindowsBound = Option<string>.From(() => loader("WindowsBound", null)).Map(rectFromString),
                State = (FormWindowState) Enum.Parse(typeof (FormWindowState), loader("WindowsState", "Normal")),
                IsShowCheckedBackground = Convert.ToBoolean(loader("IsShowCheckedBackground", "false")),
                IsShowToolBar = Convert.ToBoolean(loader("IsShowToolBar", "false")),
                IsShowThumbnail = Convert.ToBoolean(loader("IsShowThumbnail", "false"))
            };
        }
        public void Save(Action<string,string> save){
            Contract.Requires(save != null);
            save("igVersion", IgVersion);
            WindowsBound.Do(rect => save("WindowsBound", rectToString(rect)));
            save("WindowsState", State.ToString());
            save("IsShowCheckedBackground", IsShowCheckedBackground.ToString());
            save("IsShowToolBar", IsShowToolBar.ToString());
            save("IsShowThumbnail", IsShowThumbnail.ToString());
        }
        static string rectToString(Rectangle rc) => $"{rc.Left},{rc.Top},{rc.Width},{rc.Height}";
        static Rectangle rectFromString(string s){
            Contract.Requires(!string.IsNullOrEmpty(s));
            Contract.Requires(s.Count(c => c == ',') == 3);
            var rectTexts = s.Split(',');
            return new Rectangle(
                int.Parse(rectTexts[0]),
                int.Parse(rectTexts[1]),
                int.Parse(rectTexts[2]),
                int.Parse(rectTexts[3])
                );
        }
    }
}
