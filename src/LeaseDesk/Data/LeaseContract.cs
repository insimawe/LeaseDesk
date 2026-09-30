using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LeaseDesk.Data;

public class LeaseContract
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    [Display(Name = "Lessor name")]
    public string LessorName { get; set; } = "";

    [MaxLength(200)]
    [Display(Name = "Representative")]
    public string LessorRepresentative { get; set; } = "";

    [Required]
    [MaxLength(400)]
    [Display(Name = "Lessor address")]
    public string LessorAddress { get; set; } = "";

    [Required]
    [MaxLength(200)]
    [Display(Name = "Lessee name")]
    public string LesseeName { get; set; } = "";

    [Required]
    [MaxLength(400)]
    [Display(Name = "Lessee address")]
    public string LesseeAddress { get; set; } = "";

    [Required]
    [MaxLength(200)]
    [Display(Name = "Property")]
    public string PropertyName { get; set; } = "";

    [Required]
    [MaxLength(200)]
    [Display(Name = "Unit")]
    public string Unit { get; set; } = "";

    [Required]
    [MaxLength(400)]
    [Display(Name = "Property address")]
    public string PropertyAddress { get; set; } = "";

    [Required]
    [MaxLength(80)]
    [Display(Name = "Unit type")]
    public string UnitType { get; set; } = "Studio";

    [Range(1, 20)]
    [Display(Name = "Maximum occupants")]
    public int MaxOccupants { get; set; } = 3;

    [Display(Name = "Start date")]
    public DateOnly StartDate { get; set; }

    [Range(1, 30)]
    [Display(Name = "Term (years)")]
    public int TermYears { get; set; } = 3;

    [Display(Name = "Signing date")]
    public DateOnly SigningDate { get; set; }

    [Required]
    [MaxLength(200)]
    [Display(Name = "Signing place")]
    public string SigningPlace { get; set; } = "";

    [Range(typeof(decimal), "0.01", "999999999")]
    [Display(Name = "Monthly rent")]
    public decimal MonthlyRent { get; set; }

    [Range(0, 120)]
    [Display(Name = "Post-dated checks")]
    public int PostDatedCheckCount { get; set; } = 35;

    [Range(0, 24)]
    [Display(Name = "Advance (months)")]
    public int AdvanceMonths { get; set; } = 1;

    [Range(0, 24)]
    [Display(Name = "Security deposit (months)")]
    public int SecurityDepositMonths { get; set; } = 2;

    [Range(typeof(decimal), "0", "999999999")]
    [Display(Name = "Amount received")]
    public decimal ReceiptAmount { get; set; }

    [Display(Name = "Date received")]
    public DateOnly ReceiptDate { get; set; }

    [MaxLength(200)]
    [Display(Name = "Payer")]
    public string ReceiptPayer { get; set; } = "";

    [Required]
    [MaxLength(200)]
    [Display(Name = "First witness")]
    public string WitnessOneName { get; set; } = "";

    [Required]
    [MaxLength(200)]
    [Display(Name = "Second witness")]
    public string WitnessTwoName { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    [NotMapped]
    public DateOnly EndDate => StartDate.AddYears(TermYears);

    [NotMapped]
    public decimal AdvanceAmount => MonthlyRent * AdvanceMonths;

    [NotMapped]
    public decimal SecurityDepositAmount => MonthlyRent * SecurityDepositMonths;

    public static LeaseContract CreateHorizonDefault()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var contract = new LeaseContract
        {
            PropertyName = "Horizon 101",
            Unit = "Tower 2, unit 34A",
            PropertyAddress = "Mango Avenue, Cebu City",
            UnitType = "Studio",
            MaxOccupants = 3,
            StartDate = today,
            TermYears = 3,
            SigningDate = today,
            SigningPlace = "Cebu City, Cebu, Philippines",
            MonthlyRent = 15_000m,
            PostDatedCheckCount = 35,
            AdvanceMonths = 1,
            SecurityDepositMonths = 2,
            ReceiptDate = today,
            WitnessOneName = "Maria Teresa Aquino",
            WitnessTwoName = "Rosenda Akut"
        };
        contract.ReceiptAmount = contract.AdvanceAmount + contract.SecurityDepositAmount;
        return contract;
    }
}
