using FluentValidation;
using MTL.APP.Models;
public class HangHoaCVJPValidator : AbstractValidator<CvjpSanpham>
{
	public HangHoaCVJPValidator()
	{
		RuleFor(x => x.Tensp)
			  .NotEmpty()
			  .WithMessage(customer => string.Format("Tên hàng hóa không được để trống."));
		RuleFor(x => x.Idnhomhang)
			  .NotEmpty()
			  .WithMessage(customer => string.Format("Nhóm hàng không được để trống."));


	}

	public Func<object, string, Task<IEnumerable<string>>> ValidateValue => async (model, propertyName) =>
	{
		var result = await ValidateAsync(ValidationContext<CvjpSanpham>.CreateWithOptions((CvjpSanpham)model, x => x.IncludeProperties(propertyName)));
		if (result.IsValid)
			return Array.Empty<string>();
		return result.Errors.Select(e => e.ErrorMessage);
	};
}
