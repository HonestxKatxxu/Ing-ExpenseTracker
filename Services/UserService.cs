using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ing_ExpenseTracker.Models;

public class UserService
{
    private static readonly string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    private static readonly string FolderPath = Path.Combine(DesktopPath, "LocalDB");
    private static readonly string FilePath = Path.Combine(FolderPath, "users.json");
    private static readonly string CurrentUserPath = Path.Combine(FolderPath, "current_user.json");

    private User _currentUser;

    // Load user data from the JSON file
    public AppData LoadUsers()
    {
        if (!File.Exists(FilePath))
            return new AppData();

        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<AppData>(json) ?? new AppData();
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Error deserializing JSON: {ex.Message}");
            return new AppData();
        }
    }

    // Save user data to the JSON file
    public void SaveUsers(AppData data)
    {
        if (!Directory.Exists(FolderPath))
        {
            Directory.CreateDirectory(FolderPath);
        }

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }

    // Load the currently logged-in user
    public User LoadCurrentUser()
    {
        if (!File.Exists(CurrentUserPath))
            return null;

        try
        {
            var json = File.ReadAllText(CurrentUserPath);
            return JsonSerializer.Deserialize<User>(json);
        }
        catch
        {
            return null;
        }
    }

    // Save the currently logged-in user
    public void SaveCurrentUser()
    {
        if (_currentUser != null)
        {
            var json = JsonSerializer.Serialize(_currentUser, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(CurrentUserPath, json);
        }
    }

    // Get the currently logged-in user
    public User GetCurrentUser()
    {
        if (_currentUser == null)
        {
            _currentUser = LoadCurrentUser();
        }
        return _currentUser;
    }

    // Set the current user
    public void SetCurrentUser(User user)
    {
        _currentUser = user;
        SaveCurrentUser();
    }

    // Logout the current user
    public void Logout()
    {
        _currentUser = null;
        if (File.Exists(CurrentUserPath))
        {
            File.Delete(CurrentUserPath);
        }
    }

    // Hash a password using SHA256
    public string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    // Validate a password by comparing the hashed input with the stored hash
    public bool ValidatePassword(string inputPassword, string storedPassword)
    {
        var hashedInputPassword = HashPassword(inputPassword);
        return hashedInputPassword == storedPassword;
    }

    // Calculate the main balance for a user
    public decimal CalculateBalance(AppData data)
    {
        var currentUser = GetCurrentUser();
        if (currentUser == null) throw new InvalidOperationException("No user logged in.");

        var userTransactions = data.Transactions.Where(t => t.UserId == currentUser.Id).ToList();
        decimal totalCredit = userTransactions.Sum(t => t.Credit);
        decimal totalDebit = userTransactions.Sum(t => t.Debit);
        return totalCredit - totalDebit;
    }

    // Calculate debt clearing and remaining amounts for a user
    public (decimal Cleared, decimal Remaining) CalculateDebt(AppData data)
    {
        var currentUser = GetCurrentUser();
        if (currentUser == null) throw new InvalidOperationException("No user logged in.");

        var userDebts = data.Debts.Where(d => d.UserId == currentUser.Id).ToList();
        decimal totalDebt = userDebts.Sum(d => d.Amount);
        decimal totalPaid = userDebts.Sum(d => d.PaidAmount);
        return (totalPaid, totalDebt - totalPaid);
    }

    // Check if a username is already in use
    public bool IsUsernameTaken(string username, AppData data)
    {
        return data.Users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
    }

    // Create a new user
    public void CreateUser(string username, string password, string email, AppData data)
    {
        if (IsUsernameTaken(username, data))
        {
            throw new InvalidOperationException("Username already exists.");
        }

        var newUser = new User
        {
            Id = data.Users.Any() ? data.Users.Max(u => u.Id) + 1 : 1, // Assign unique ID
            Username = username,
            Password = HashPassword(password),
            Email = email
        };

        data.Users.Add(newUser);
        SaveUsers(data);

        // Set the new user as the current user
        SetCurrentUser(newUser);
    }

    // Update user information
    public void UpdateUser(User updatedUser, AppData data)
    {
        var user = data.Users.FirstOrDefault(u => u.Id == updatedUser.Id);
        if (user != null)
        {
            user.Username = updatedUser.Username;
            user.Email = updatedUser.Email;
            SaveUsers(data);
        }
    }

    // Delete a user by ID
    public void DeleteUser(int userId, AppData data)
    {
        var user = data.Users.FirstOrDefault(u => u.Id == userId);
        if (user != null)
        {
            data.Users.Remove(user);
            SaveUsers(data);
        }
    }

    // Login a user
    public bool Login(string username, string password, AppData data)
    {
        var user = data.Users.FirstOrDefault(u => u.Username == username);
        if (user != null && ValidatePassword(password, user.Password))
        {
            SetCurrentUser(user); // Set the logged-in user as the current user
            return true;
        }
        return false;
    }
}
