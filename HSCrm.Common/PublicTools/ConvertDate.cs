using MD.PersianDateTime.Standard;

namespace SinaPharm.Common
{
    public static class ConvertDate
    {
        public static DateTime ConvertShamsiToMiladi(this string date)
        {
            PersianDateTime persianDate = PersianDateTime.Parse(date);
            return persianDate.ToDateTime();
        }

        public static string ConvertMiladiToShamsi(this DateTime date, string format)
        {
            PersianDateTime persianDate = new PersianDateTime(date);
            return persianDate.ToString(format);
        }

        public static string ConvertMiladiToShamsiShort(this DateTime date)
        {
            PersianDateTime persianDate = new PersianDateTime(date);
            return persianDate.ToShortDateTimeString();
        }
    }
}
