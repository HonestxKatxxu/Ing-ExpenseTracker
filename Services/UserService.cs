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
    private static readonly string FolderPath = Path.Combine(DesktopPath, "Ing expense");
    private static readonly string FilePath = Path.Combine(FolderPath, "users.json");
    private static readonly string CurrentUserPath = Path.Combine(FolderPath, "current_user.json");

    private User _currentUser;
    public static Action<decimal> OnRemainingDebtUpdated;
    public static Action<decimal> OnNetBalanceUpdated;
    private decimal _netBalance;
    public decimal NetBalance => _netBalance;
    public static Action<decimal> OnNetBalanceChanged;


    public void UpdateDashboardRemainingDebt(decimal remainingDebt)
    {
        OnRemainingDebtUpdated?.Invoke(remainingDebt);
    }
    // Update NetBalance and trigger the event
    public void UpdateNetBalance(decimal newBalance)
    {
        _netBalance = newBalance;

        var data = LoadUsers();
        var currentUser = GetCurrentUser();

        var user = data.Users.FirstOrDefault(u => u.Id == currentUser.Id);
        if (user != null)
        {
            user.NetBalance = _netBalance;
            SaveUsers(data);


            // Notify all subscribers of the NetBalance change
            OnNetBalanceChanged?.Invoke(_netBalance);
        }
    }
    public void UpdateNetBalanceAfterTransaction(decimal creditAmount, decimal debitAmount)
    {
        var currentUser = GetCurrentUser();
        if (currentUser == null) return;

        var data = LoadUsers();
        var user = data.Users.FirstOrDefault(u => u.Id == currentUser.Id);
        if (user != null)
        {
            // Update NetBalance
            _netBalance += creditAmount - debitAmount;
            user.NetBalance = _netBalance;

            // Persist changes to file
            SaveUsers(data);
        }
    }


    public void RecalculateNetBalance()
    {
        var currentUser = GetCurrentUser();
        if (currentUser == null) return;

        var data = LoadUsers();
        var userTransactions = data.Transactions.Where(t => t.UserId == currentUser.Id);
        var userDebts = data.Debts.Where(d => d.UserId == currentUser.Id);

        _netBalance = userTransactions.Sum(t => t.Credit - t.Debit)
                 + userDebts.Where(d => d.Status == "Pending").Sum(d => d.Amount);

        var user = data.Users.FirstOrDefault(u => u.Id == currentUser.Id);
        if (user != null)
        {
            user.NetBalance = _netBalance;
            SaveUsers(data);

            // Notify all subscribers of the NetBalance change
            OnNetBalanceChanged?.Invoke(_netBalance);
        }
    }

    public string FormatWithCurrency(decimal amount, string currencyCode)
    {
        return currencyCode switch
        {
            "USD" => $"${amount:F2}",
            "EUR" => $"€{amount:F2}",
            "INR" => $"₹{amount:F2}",
            "GBP" => $"£{amount:F2}",
            "JPY" => $"¥{amount:F2}",
            _ => amount.ToString("F2") // Default formatting
        };
    }

    public void AddCashInflow(Transaction transaction)
    {
        var data = LoadUsers();
        transaction.Id = data.Transactions.Any() ? data.Transactions.Max(t => t.Id) + 1 : 1;
        transaction.Date = DateTime.Now;

        data.Transactions.Add(transaction);
        SaveUsers(data);

        _netBalance += transaction.Credit;
        UpdateNetBalance(_netBalance);
    }
    public void AddCashOutflow(Transaction transaction)
    {
        var data = LoadUsers();
        transaction.Id = data.Transactions.Any() ? data.Transactions.Max(t => t.Id) + 1 : 1;
        transaction.Date = DateTime.Now;

        data.Transactions.Add(transaction);
        SaveUsers(data);

        _netBalance -= transaction.Debit;
        UpdateNetBalance(_netBalance);
    }


    private void SaveNetBalanceToData()
    {
        var data = LoadUsers();
        var currentUser = GetCurrentUser();

        if (currentUser == null) return;

        // Save the updated NetBalance for the current user
        var user = data.Users.FirstOrDefault(u => u.Id == currentUser.Id);
        if (user != null)
        {
            user.NetBalance = _netBalance;
            SaveUsers(data);
        }
    }

    // Add Debt: Increase NetBalance since adding debt means more funds are available
    // Add Debt: Increase NetBalance since adding debt means more funds are available
    public void AddDebt(Debt debt)
    {
        var data = LoadUsers();
        var currentUser = GetCurrentUser();
        if (currentUser == null) return;

        debt.Id = data.Debts.Any() ? data.Debts.Max(d => d.Id) + 1 : 1;
        debt.UserId = currentUser.Id;
        debt.Status = "Pending";

        // Add the debt
        data.Debts.Add(debt);
        SaveUsers(data);

        // Update NetBalance: Increase NetBalance because adding debt gives more funds
        _netBalance += debt.Amount;
        UpdateNetBalance(_netBalance);

        // Notify all subscribers of the updated NetBalance
        OnNetBalanceChanged?.Invoke(_netBalance);
    }

    // Clear Debt: Decrease NetBalance since paying debt reduces available funds
    public void ClearDebt(decimal debtAmount)
    {
        var data = LoadUsers();
        var currentUser = GetCurrentUser();
        if (currentUser == null) return;

        // Update NetBalance: Decrease NetBalance because paying debt reduces funds
        _netBalance -= debtAmount;
        UpdateNetBalance(_netBalance);

        SaveUsers(data);
    }


    public void LoadNetBalance()
    {
        var data = LoadUsers();
        var currentUser = GetCurrentUser();

        if (currentUser != null)
        {
            var user = data.Users.FirstOrDefault(u => u.Id == currentUser.Id);
            _netBalance = user?.NetBalance ?? 0;
            Console.WriteLine($"NetBalance loaded: {_netBalance} for user {currentUser.Id}");
        }
    }


    // Load all user data from the JSON file
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

    // Save all user data to the JSON file
    public void SaveUsers(AppData data)
    {
        if (!Directory.Exists(FolderPath))
        {
            Directory.CreateDirectory(FolderPath);
        }

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }

    // Load the currently logged-in user from the JSON file
    public User LoadCurrentUser()
    {
        if (!File.Exists(CurrentUserPath))
            return null;

        try
        {
            var json = File.ReadAllText(CurrentUserPath);
            return JsonSerializer.Deserialize<User>(json);
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Error deserializing current user JSON: {ex.Message}");
            return null;
        }
    }

    // Save the currently logged-in user to the JSON file
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
        return _currentUser ?? throw new InvalidOperationException("No user is currently logged in.");
    }

    // Set the current user and save to JSON
    public void SetCurrentUser(User user)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user), "User cannot be null.");

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
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    // Validate a password by comparing the hashed input with the stored hash
    public bool ValidatePassword(string inputPassword, string storedPassword)
    {
        if (string.IsNullOrWhiteSpace(inputPassword) || string.IsNullOrWhiteSpace(storedPassword))
            return false;

        var hashedInputPassword = HashPassword(inputPassword);
        return hashedInputPassword == storedPassword;
    }

    // Calculate the main balance for the current user
    public decimal CalculateBalance(AppData data)
    {
        var currentUser = GetCurrentUser();

        var userTransactions = data.Transactions.Where(t => t.UserId == currentUser.Id).ToList();
        decimal totalCredit = userTransactions.Sum(t => t.Credit);
        decimal totalDebit = userTransactions.Sum(t => t.Debit);

        return totalCredit - totalDebit;
    }

    // Calculate cleared and remaining debt for the current user
    public (decimal Cleared, decimal Remaining) CalculateDebt(AppData data)
    {
        var currentUser = GetCurrentUser();

        var userDebts = data.Debts.Where(d => d.UserId == currentUser.Id).ToList();
        decimal totalDebt = userDebts.Sum(d => d.Amount);
        decimal totalPaid = userDebts.Sum(d => d.PaidAmount);

        return (totalPaid, totalDebt - totalPaid);
    }

    // Check if a username already exists
    public bool IsUsernameTaken(string username, AppData data)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be null or empty.", nameof(username));

        return data.Users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
    }

    // Create a new user
    public void CreateUser(string username, string password, string email, AppData data)
    {
        if (IsUsernameTaken(username, data))
            throw new InvalidOperationException("Username already exists.");

        var newUser = new User
        {
            Id = data.Users.Any() ? data.Users.Max(u => u.Id) + 1 : 1,
            Username = username,
            Password = HashPassword(password),
            Email = email,
            CreatedOn = DateTime.Now
        };

        data.Users.Add(newUser);
        SaveUsers(data);

        // Set the newly created user as the current user
        SetCurrentUser(newUser);
    }

    // Update an existing user's information
    public void UpdateUser(User updatedUser, AppData data)
    {
        if (updatedUser == null)
            throw new ArgumentNullException(nameof(updatedUser), "Updated user cannot be null.");

        var user = data.Users.FirstOrDefault(u => u.Id == updatedUser.Id);
        if (user != null)
        {
            user.Username = updatedUser.Username;
            user.Email = updatedUser.Email;
            SaveUsers(data);
        }
        else
        {
            throw new InvalidOperationException("User not found.");
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
        else
        {
            throw new InvalidOperationException("User not found.");
        }
    }

    // Login a user and set them as the current user
    public bool Login(string username, string password, AppData data)
    {
        var user = data.Users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        if (user != null && ValidatePassword(password, user.Password))
        {
            SetCurrentUser(user);
            return true;
        }
        return false;
    }

    // Get transactions for the current user
    public List<Transaction> GetUserTransactions(AppData data)
    {
        var currentUser = GetCurrentUser();
        return data.Transactions.Where(t => t.UserId == currentUser.Id).ToList();
    }
    public void SaveTransaction(Transaction transaction)
    {
        var data = LoadUsers();
        data.Transactions.Add(transaction);
        SaveUsers(data);
    }

    public List<Transaction> GetTransactionsForCurrentUser()
    {
        var currentUser = GetCurrentUser();
        return LoadUsers().Transactions.Where(t => t.UserId == currentUser.Id).ToList();
    }

    // Get debts for the current user
    public List<Debt> GetUserDebts(AppData data)
    {
        var currentUser = GetCurrentUser();
        return data.Debts.Where(d => d.UserId == currentUser.Id).ToList();
    }

    public void GenerateReceipt(Transaction transaction)
    {
        string receiptPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"Receipt_{transaction.Id}.pdf");

        using (var stream = new FileStream(receiptPath, FileMode.Create))
        {
            var writer = new PdfSharp.Pdf.PdfDocument();
            var page = writer.AddPage();
            var graphics = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

            // Fonts and styling
            var titleFont = new PdfSharp.Drawing.XFont("Arial", 18);
            var headerFont = new PdfSharp.Drawing.XFont("Arial", 14);
            var contentFont = new PdfSharp.Drawing.XFont("Arial", 12);



            // Header
            graphics.DrawString("Transaction Receipt", titleFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XPoint(200, 50));

            // Draw a line
            graphics.DrawLine(PdfSharp.Drawing.XPens.Black, 50, 70, 550, 70);

            // Transaction details in table-like format
            graphics.DrawString("Transaction Details", headerFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XPoint(50, 100));

            var yPosition = 140; // Start of details
            var lineSpacing = 30; // Space between lines

            graphics.DrawString("Transaction ID:", headerFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XPoint(50, yPosition));
            graphics.DrawString(transaction.Id.ToString(), contentFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XPoint(200, yPosition));

            yPosition += lineSpacing;
            graphics.DrawString("Date:", headerFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XPoint(50, yPosition));
            graphics.DrawString(transaction.Date.ToString("MM/dd/yyyy hh:mm:ss tt"), contentFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XPoint(200, yPosition));

            yPosition += lineSpacing;
            graphics.DrawString("Type:", headerFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XPoint(50, yPosition));
            graphics.DrawString(transaction.Type ?? "N/A", contentFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XPoint(200, yPosition));


            yPosition += lineSpacing;
            graphics.DrawString("Amount:", headerFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XPoint(50, yPosition));
            graphics.DrawString((transaction.Credit > 0 ? transaction.Credit : transaction.Debit).ToString("C"), contentFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XPoint(200, yPosition));

            yPosition += lineSpacing;
            graphics.DrawString("Description:", headerFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XPoint(50, yPosition));
            graphics.DrawString(transaction.Description, contentFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XPoint(200, yPosition));

            // Footer section
            yPosition += (lineSpacing * 2);
            graphics.DrawLine(PdfSharp.Drawing.XPens.Black, 50, yPosition, 550, yPosition);
            yPosition += lineSpacing;
            graphics.DrawString("Thank you for using Ing_ExpenseTracker", contentFont, PdfSharp.Drawing.XBrushes.Gray, new PdfSharp.Drawing.XPoint(50, yPosition));

            // Save the receipt
            writer.Save(stream);
        }

        Console.WriteLine($"Receipt generated at {receiptPath}");
    }


}
