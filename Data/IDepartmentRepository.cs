using AScheduler.Domain;

namespace AScheduler.Data
{
    /// <summary>
    /// Repository interface for department management and governance policies.
    /// </summary>
    public interface IDepartmentRepository
    {
        /// <summary>
        /// Retrieves all departments.
        /// </summary>
        Task<List<Department>> GetAllAsync();

        /// <summary>
        /// Retrieves a department by ID.
        /// </summary>
        Task<Department?> GetByIdAsync(int departmentId);

        /// <summary>
        /// Retrieves a department by name.
        /// </summary>
        Task<Department?> GetByNameAsync(string name);

        /// <summary>
        /// Creates a new department.
        /// </summary>
        /// <returns>The newly created department ID.</returns>
        Task<int> CreateAsync(string name, string? description, string contactEmail, int retryPolicy, int logRetentionDays);

        /// <summary>
        /// Updates an existing department.
        /// </summary>
        /// <returns>True if update succeeded, false if department not found.</returns>
        Task<bool> UpdateAsync(int departmentId, string name, string? description, string contactEmail, int retryPolicy, int logRetentionDays);

        /// <summary>
        /// Deletes a department (soft delete or physical removal depending on policy).
        /// </summary>
        /// <returns>True if deletion succeeded, false if department not found.</returns>
        Task<bool> DeleteAsync(int departmentId);

        /// <summary>
        /// Retrieves the retry policy for a department.
        /// Returns 0 (RequireApproval), 1 (Auto), or 2 (ManualOnly).
        /// </summary>
        Task<int?> GetRetryPolicyAsync(int departmentId);

        /// <summary>
        /// Gets all boxes assigned to a department.
        /// </summary>
        Task<List<BoxDefinition>> GetDepartmentBoxesAsync(int departmentId);

        /// <summary>
        /// Gets all users assigned to a department.
        /// </summary>
        Task<List<string>> GetDepartmentUsersAsync(int departmentId);

        /// <summary>
        /// Gets the "Default" department (always exists).
        /// </summary>
        Task<Department?> GetDefaultDepartmentAsync();
    }
}
