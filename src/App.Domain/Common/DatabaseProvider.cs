namespace App.Domain.Common;

/// <summary>
/// SQL dialect / driver used by the DB manager.
/// AwsRds currently means "SQL Server hosted on AWS RDS"; switch to
/// <see cref="Oracle"/> for an Oracle RDS instance or any Oracle database.
/// </summary>
public enum DatabaseProvider
{
    SqlServer = 0,
    AwsRds = 1,
    Oracle = 2,
}
