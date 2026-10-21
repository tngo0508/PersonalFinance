namespace PersonalFinance.Shared.DTOs.SavingsGoals;

public class SavingsGoalDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public string? Description { get; set; }
    public DateTime? TargetDate { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public double ProgressPercent { get; set; }
}

public class CreateSavingsGoalRequestDto
{
    public string Name { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public decimal? CurrentAmount { get; set; }
    public string? Description { get; set; }
    public DateTime? TargetDate { get; set; }
}

public class UpdateSavingsGoalRequestDto
{
    public string? Name { get; set; }
    public decimal? TargetAmount { get; set; }
    public decimal? CurrentAmount { get; set; }
    public string? Description { get; set; }
    public DateTime? TargetDate { get; set; }
    public bool IsCompleted { get; set; }
}

public class CreateSavingsGoalResponseDto
{
    public bool Success { get; set; }
    public int Id { get; set; }
}

public class UpdateSavingsGoalResponseDto
{
    public bool Success { get; set; }
}

public class DeleteSavingsGoalResponseDto
{
    public bool Success { get; set; }
}
