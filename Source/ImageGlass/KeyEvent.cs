using System;
using System.Linq;
using System.Windows.Forms;

namespace ImageGlass {
    static class KeyEvent {
        public static bool Key(KeyEventArgs e, params Keys[] keys) {
            return !e.Control & !e.Shift & !e.Alt & keys.Contains(e.KeyCode);
        }
        public static bool CtrlKey(KeyEventArgs e, params Keys[] keys) {
            return e.Control & !e.Shift & !e.Alt & keys.Contains(e.KeyCode);
        }
        public static bool AltKey(KeyEventArgs e, params Keys[] keys) {
            return !e.Control & !e.Shift & e.Alt & keys.Contains(e.KeyCode);
        }
    }
}
