using System.Globalization;
using System.Windows.Data;

namespace EsnafPos.Helpers
{
    /// <summary>
    /// OrderChangeLog.Reason'da saklanan ASCII değeri ("Urun Iptali"/"Urun Degisimi")
    /// ekranda düzgün Türkçe gösterir. Saklanan değer değişmez — bu sayede ChangeLog
    /// renklendirme DataTrigger'ları ve eski kayıtlar bozulmadan çalışmaya devam eder.
    /// </summary>
    public class ReasonDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value as string) switch
            {
                "Urun Iptali"   => "Ürün İptali",
                "Urun Degisimi" => "Ürün Değişimi",
                var other       => other ?? ""
            };

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
