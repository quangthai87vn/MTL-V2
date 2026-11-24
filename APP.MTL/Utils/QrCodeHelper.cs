using QRCoder;

namespace MTL.APP.Utils;

public static class QrCodeHelper
{
	public static string ToDataUrlPng(string? text, int pixelsPerModule = 8)
	{
		var payload = string.IsNullOrWhiteSpace(text) ? "-" : text!;
		using var gen = new QRCodeGenerator();
		using var data = gen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
		var png = new PngByteQRCode(data);
		var bytes = png.GetGraphic(pixelsPerModule);
		return "data:image/png;base64," + Convert.ToBase64String(bytes);
	}
}
