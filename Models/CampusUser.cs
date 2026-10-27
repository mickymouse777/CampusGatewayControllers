using Azure;
using Azure.Data.Tables;
namespace CampusGatewayControllers.Models;

// This is the shape of one saved user in Azure Table Storage.
// ITableEntity asks us to include the two keys plus storage metadata.
public class CampusUser : ITableEntity
{
    // A partition groups related rows. Here every registered account is a Student.
    public string PartitionKey { get; set; } = "Student";
    // Within that partition, the normalized email uniquely identifies the user.
    public string RowKey { get; set; } = "";
    public string FullName { get; set; } = "";
    // This is a hash, not a recoverable copy of the password. Never return it to clients.
    public string PasswordHash { get; set; } = "";
    // We set this ourselves at registration so we know when the account was created.
    public DateTimeOffset CreatedDate { get; set; }
    // Table Storage maintains Timestamp and ETag. ETag can help detect conflicting
    // updates if you add profile editing later; we do not edit profiles in this sample.
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
