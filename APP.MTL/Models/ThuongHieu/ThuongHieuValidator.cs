using FluentValidation;
using MTL.APP.Models;
public class ThuongHieuValidator : AbstractValidator<TblThuonghieu>
{
	public ThuongHieuValidator()
	{
		RuleFor(x => x.Thuonghieu)
			  .NotEmpty()
			  .WithMessage(customer => string.Format("Tên thương hiệu không được để trống."));


	}

	public Func<object, string, Task<IEnumerable<string>>> ValidateValue => async (model, propertyName) =>
	{
		var result = await ValidateAsync(ValidationContext<TblThuonghieu>.CreateWithOptions((TblThuonghieu)model, x => x.IncludeProperties(propertyName)));
		if (result.IsValid)
			return Array.Empty<string>();
		return result.Errors.Select(e => e.ErrorMessage);
	};
}
