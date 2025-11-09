using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace HSCrm.Common.PublicTools
{
    public static class StringCleaner
    {
        /// <summary>
        /// تبدیل اعداد فارسی و عربی به انگلیسی (0-9)
        /// </summary>
        public static string NormalizeDigits(this string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input ?? string.Empty;

            var persian = new[] { '۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹' };
            var arabic = new[] { '٠', '١', '٢', '٣', '٤', '٥', '٦', '٧', '٨', '٩' };
            var sb = new StringBuilder(input.Length);

            foreach (var ch in input)
            {
                if (ch >= '0' && ch <= '9')
                {
                    sb.Append(ch);
                    continue;
                }

                var idxP = Array.IndexOf(persian, ch);
                if (idxP >= 0)
                {
                    sb.Append((char)('0' + idxP));
                    continue;
                }

                var idxA = Array.IndexOf(arabic, ch);
                if (idxA >= 0)
                {
                    sb.Append((char)('0' + idxA));
                    continue;
                }

                sb.Append(ch);
            }

            return sb.ToString();
        }

        /// <summary>
        /// پاکسازی رشته برای استفاده به عنوان UserName:
        /// حذف فاصله‌ها، اعداد فارسی/عربی و کاراکترهای غیر مجاز.
        /// فقط حروف، ارقام و کاراکترهای -._@+ را نگه می‌دارد.
        /// </summary>
        public static string CleanUserName(this string perno)
        {
            if (string.IsNullOrWhiteSpace(perno))
                return string.Empty;

            var s = NormalizeDigits(perno).Trim();

            // حذف تمام فاصله‌ها و کاراکترهای whitespace
            s = Regex.Replace(s, @"\s+", "");

            // حذف کاراکترهای غیر مجاز (غیر از حروف، ارقام و -._@+)
            s = Regex.Replace(s, @"[^A-Za-z0-9\-\._@+]", "");

            return s;
        }

        public static string CleanName(this string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            // 1) حذف فاصله‌های اول و آخر
            var s = NormalizeDigits(name).Trim();

            // 2) تبدیل ي و ك عربی به ی و ک فارسی
            s = s.Replace('ي', 'ی').Replace('ك', 'ک');

            // 3) مجاز کردن حروف فارسی، عربی، انگلیسی، اعداد، فاصله، و -._@+
            // یعنی فقط کاراکترهای غیرمجاز حذف می‌شن (نه فاصله وسط)
            s = Regex.Replace(s, @"[^A-Za-z0-9\u0600-\u06FF\-\._@+ ]", "");

            return s;
        }

        public static string DisplayNameAttribute(this Enum value, DisplayProperty property = DisplayProperty.Name)
        {
            var attribute = value.GetType().GetField(value.ToString())
                .GetCustomAttributes<DisplayAttribute>(false).FirstOrDefault();

            if (attribute == null)
                return value.ToString();

            var propValue = attribute.GetType().GetProperty(property.ToString()).GetValue(attribute, null);
            return propValue.ToString();
        }

        public enum DisplayProperty
        {
            Description,
            Name
        }
    }
}
