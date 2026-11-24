using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MTL.APP.Utils
{
	public static class StringUtils
	{
		public static string ConvertToSlug(string input)
		{
			// Chuyển đổi các ký tự có dấu thành các ký tự không dấu
			string normalizedString = input.Normalize(NormalizationForm.FormD);
			StringBuilder stringBuilder = new StringBuilder();

			foreach (char c in normalizedString)
			{
				if (c == 'Đ' || c == 'đ') // Kiểm tra ký tự "Đ" hoặc "đ"
				{
					stringBuilder.Append('d'); // Chuyển đổi "Đ" thành "d"
				}
				else
				{
					UnicodeCategory unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
					if (unicodeCategory != UnicodeCategory.NonSpacingMark)
					{
						stringBuilder.Append(c);
					}
				}
			}

			// Chuyển đổi các ký tự thành chữ thường
			string lowercase = stringBuilder.ToString().ToLower();

			// Loại bỏ các ký tự đặc biệt và khoảng trắng, thay thế bằng dấu gạch ngang
			string slug = Regex.Replace(lowercase, @"[^a-z0-9\s-]", ""); // Chỉ giữ lại chữ cái, số, khoảng trắng và dấu gạch ngang
			slug = Regex.Replace(slug, @"\s+", " ").Trim(); // Loại bỏ khoảng trắng thừa
			slug = slug.Replace(" ", "-"); // Thay thế khoảng trắng bằng dấu gạch ngang
			return slug;
		}
	}
}
