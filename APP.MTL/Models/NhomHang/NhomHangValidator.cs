using FluentValidation;
using MTL.APP.Models;
public class NhomHangValidator : AbstractValidator<TblNhomhang>
{
	public NhomHangValidator()
	{
		RuleFor(x => x.Nhomhang)
			  .NotEmpty()
			  .WithMessage(customer => string.Format("Nhóm hàng không được để trống."));

		RuleFor(x => x.Viettat)
			  .NotEmpty()
			  .WithMessage(customer => string.Format("Tên viết tắt không được để trống."));
	}

	public Func<object, string, Task<IEnumerable<string>>> ValidateValue => async (model, propertyName) =>
	{
		var result = await ValidateAsync(ValidationContext<TblNhomhang>.CreateWithOptions((TblNhomhang)model, x => x.IncludeProperties(propertyName)));
		if (result.IsValid)
			return Array.Empty<string>();
		return result.Errors.Select(e => e.ErrorMessage);
	};
}
