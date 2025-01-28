using FluentValidation;
using pgaas.Controllers.Dto.Clusters;

namespace pgaas.backend.Validators;

public class CreateClusterDtoValidator : AbstractValidator<CreateClusterDto>
{
	public CreateClusterDtoValidator()
	{
		RuleFor(x => x.SystemName)
			.NotEmpty().WithMessage("SystemName is required.")
			.Matches("^[a-zA-Z0-9-]+$").WithMessage("SystemName can only contain letters, numbers, or hyphens.")
			.MaximumLength(31).WithMessage("SystemName cannot exceed 31 characters.");

		RuleFor(x => x.StorageSize)
			.InclusiveBetween(1, 5).WithMessage("StorageSize must be between 1 and 5.");

		RuleFor(x => x.Cpu)
			.InclusiveBetween(100, 800).WithMessage("Cpu must be between 100 and 800.");

		RuleFor(x => x.Memory)
			.InclusiveBetween(300, 1500).WithMessage("Memory must be between 300 and 1500.");

		RuleFor(x => x.MajorVersion)
			.InclusiveBetween(13, 17).WithMessage("MajorVersion must be between 13 and 17.");

		RuleFor(x => x.DatabaseName)
			.NotEmpty().WithMessage("DatabaseName is required.")
			.Matches("^[a-zA-Z0-9-]+$").WithMessage("DatabaseName can only contain letters, numbers, or hyphens.")
			.MaximumLength(31).WithMessage("DatabaseName cannot exceed 31 characters.");

		RuleFor(x => x.Instances)
			.InclusiveBetween(1, 3).WithMessage("Instances must be between 1 and 3.");

		RuleFor(x => x.OwnerName)
			.NotEmpty().WithMessage("OwnerName is required.")
			.Matches("^[a-zA-Z0-9-]+$").WithMessage("OwnerName can only contain letters, numbers, or hyphens.")
			.MaximumLength(31).WithMessage("OwnerName cannot exceed 31 characters.");
	}
}