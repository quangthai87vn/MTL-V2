using FluentValidation;
using MTL.APP.Models;
public class KhachHangValidator : AbstractValidator<TblUser>
{
	public KhachHangValidator()
	{
		RuleFor(x => x.Dienthoai)
			  .NotEmpty()
			  .WithMessage(customer => string.Format("Điện thoại không được để trống."));
		RuleFor(x => x.Fullname)
			  .NotEmpty()
			  .WithMessage(customer => string.Format("Họ tên không được để trống."));
		//cho phép null
		RuleFor(x => x.Email).EmailAddress().When(x => x.Email != "")
		   .WithMessage("Địa chỉ Email không hợp lệ !");

	}

	public Func<object, string, Task<IEnumerable<string>>> ValidateValue => async (model, propertyName) =>
	{
		var result = await ValidateAsync(ValidationContext<TblUser>.CreateWithOptions((TblUser)model, x => x.IncludeProperties(propertyName)));
		if (result.IsValid)
			return Array.Empty<string>();
		return result.Errors.Select(e => e.ErrorMessage);
	};
}
