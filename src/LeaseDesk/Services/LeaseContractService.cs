using LeaseDesk.Data;
using Microsoft.EntityFrameworkCore;

namespace LeaseDesk.Services;

public class LeaseContractService(LeaseDeskDbContext db)
{
    public async Task<IReadOnlyList<LeaseContract>> SearchAsync(string? query)
    {
        var contracts = db.Contracts.AsNoTracking();
        var term = query?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var pattern = $"%{term}%";
            contracts = contracts.Where(contract =>
                EF.Functions.Like(contract.LesseeName, pattern)
                || EF.Functions.Like(contract.LessorName, pattern)
                || EF.Functions.Like(contract.PropertyName, pattern)
                || EF.Functions.Like(contract.Unit, pattern)
                || EF.Functions.Like(contract.PropertyAddress, pattern));
        }

        return await contracts
            .OrderByDescending(contract => contract.UpdatedAtUtc)
            .ToListAsync();
    }

    public Task<LeaseContract?> GetAsync(int id) =>
        db.Contracts.AsNoTracking().FirstOrDefaultAsync(contract => contract.Id == id);

    public async Task<LeaseContract> SaveAsync(LeaseContract contract)
    {
        var now = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(contract.ReceiptPayer))
            contract.ReceiptPayer = contract.LesseeName.Trim();

        if (contract.Id == 0)
        {
            contract.CreatedAtUtc = now;
            contract.UpdatedAtUtc = now;
            db.Contracts.Add(contract);
        }
        else
        {
            var existing = await db.Contracts.FirstOrDefaultAsync(item => item.Id == contract.Id)
                ?? throw new InvalidOperationException("That contract no longer exists.");

            contract.CreatedAtUtc = existing.CreatedAtUtc;
            contract.UpdatedAtUtc = now;
            db.Entry(existing).CurrentValues.SetValues(contract);
        }

        await db.SaveChangesAsync();
        return contract;
    }

    public async Task<int> CopyAsync(int id)
    {
        var source = await db.Contracts.AsNoTracking().FirstOrDefaultAsync(contract => contract.Id == id)
            ?? throw new InvalidOperationException("That contract no longer exists.");

        var now = DateTime.UtcNow;
        var copy = new LeaseContract
        {
            LessorName = source.LessorName,
            LessorRepresentative = source.LessorRepresentative,
            LessorAddress = source.LessorAddress,
            LesseeName = source.LesseeName,
            LesseeAddress = source.LesseeAddress,
            PropertyName = source.PropertyName,
            Unit = source.Unit,
            PropertyAddress = source.PropertyAddress,
            UnitType = source.UnitType,
            MaxOccupants = source.MaxOccupants,
            StartDate = source.StartDate,
            TermYears = source.TermYears,
            SigningDate = source.SigningDate,
            SigningPlace = source.SigningPlace,
            MonthlyRent = source.MonthlyRent,
            PostDatedCheckCount = source.PostDatedCheckCount,
            AdvanceMonths = source.AdvanceMonths,
            SecurityDepositMonths = source.SecurityDepositMonths,
            ReceiptAmount = source.ReceiptAmount,
            ReceiptDate = source.ReceiptDate,
            ReceiptPayer = source.ReceiptPayer,
            WitnessOneName = source.WitnessOneName,
            WitnessTwoName = source.WitnessTwoName,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.Contracts.Add(copy);
        await db.SaveChangesAsync();
        return copy.Id;
    }

    public async Task DeleteAsync(int id)
    {
        var contract = await db.Contracts.FirstOrDefaultAsync(item => item.Id == id);
        if (contract is null)
            return;

        db.Contracts.Remove(contract);
        await db.SaveChangesAsync();
    }
}
