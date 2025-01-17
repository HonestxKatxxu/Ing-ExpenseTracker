using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ing_ExpenseTracker.Models;
using Microsoft.AspNetCore.Components;

namespace Ing_ExpenseTracker.Components.Pages.Dashboard
{
    public partial class Dashboard : ComponentBase, IDisposable
    {
        private AppData Data = new AppData();
        private decimal TotalDebt = 0;
        private decimal RemainingDebt = 0;
        private List<Debt> PendingDebts = new List<Debt>();
        private List<Transaction> FilteredTransactions = new List<Transaction>();
        private DateTime StartDate = DateTime.Today.AddMonths(-1); // Default to last month
        private DateTime EndDate = DateTime.Today; // Default to today

        private string UserName = "Guest"; // Default name
        private decimal TotalOutflows = 0;
        private decimal TotalInflows = 0;
        private decimal NetBalance = 0;
        private int TotalTransactions = 0;
        private decimal HighestInflow = 0;
        private decimal LowestInflow = 0;
        private decimal HighestOutflow = 0;
        private decimal LowestOutflow = 0;
        private decimal AverageTransaction = 0;

        private List<Transaction> Top5Transactions = new(); // For top 5 highest transactions

        private string SelectedCurrency = "USD"; // Default currency



        private bool IsNotificationVisible = false; // Flag to show/hide notification

        protected override void OnInitialized()
        {
            var currentUser = UserService.GetCurrentUser();
            if (currentUser == null)
            {
                NavigationManager.NavigateTo("/"); // Redirect to login if no user is logged in
                return;
            }
            UserName = currentUser.Username;
            SelectedCurrency = currentUser.PreferredCurrency ?? "USD"; // Fetch currency

            // Ensure the latest Net Balance and user data is loaded
            UserService.RecalculateNetBalance();
            LoadDashboardData();

            // Subscribe to real-time NetBalance updates
            UserService.OnNetBalanceChanged += HandleNetBalanceChanged;
        }


        // Update NetBalance when it changes
        private void HandleNetBalanceChanged(decimal updatedNetBalance)
        {
            NetBalance = updatedNetBalance;
            StateHasChanged(); // Refresh the UI
        }

        private void LoadDashboardData()
        {
            var currentUser = UserService.GetCurrentUser();
            if (currentUser == null)
            {
                NavigationManager.NavigateTo("/"); // Redirect to login if no user is logged in
                return;
            }

            // Load user-specific data
            UserService.LoadNetBalance(); // Ensure the latest NetBalance is loaded

            Data = UserService.LoadUsers();

          

            var userTransactions = Data.Transactions?
                .Where(t => t.UserId == currentUser.Id)
                .ToList() ?? new List<Transaction>();

            var userDebts = Data.Debts?
                .Where(d => d.UserId == currentUser.Id)
                .ToList() ?? new List<Debt>();

            UserName = currentUser.Username;

            // Load NetBalance directly from UserService
            NetBalance = UserService.NetBalance;

            // Calculate Total Inflows
            TotalInflows = userTransactions
                .Where(t => t.Credit > 0)
                .Sum(t => t.Credit);

            // Calculate Total Outflows
            TotalOutflows = userTransactions
                .Where(t => t.Debit > 0)
                .Sum(t => t.Debit);

            // Calculate Total Debt
            TotalDebt = userDebts.Sum(d => d.Amount);

            // Calculate Remaining Debt
            RemainingDebt = userDebts
                .Where(d => d.Status == "Pending")
                .Sum(d => d.Amount);

            // Get Pending Debts
            PendingDebts = userDebts
                .Where(d => d.Status == "Pending")
                .ToList();

            // Calculate Transaction Overview
            if (userTransactions.Any())
            {
                TotalTransactions = userTransactions.Count;

                var inflows = userTransactions.Where(t => t.Credit > 0).ToList();
                var outflows = userTransactions.Where(t => t.Debit > 0).ToList();

                HighestInflow = inflows.Any() ? inflows.Max(t => t.Credit) : 0;
                LowestInflow = inflows.Any() ? inflows.Min(t => t.Credit) : 0;

                HighestOutflow = outflows.Any() ? outflows.Max(t => t.Debit) : 0;
                LowestOutflow = outflows.Any() ? outflows.Min(t => t.Debit) : 0;

                AverageTransaction = userTransactions
                    .Select(t => t.Credit > 0 ? t.Credit : t.Debit)
                    .Average();
            }

            // Calculate Top 5 Highest Transactions
            Top5Transactions = userTransactions
                .OrderByDescending(t => Math.Max(t.Credit, t.Debit))
                .Take(5)
                .ToList();

            // Apply initial date filter
            ApplyDateFilter(userTransactions);
        }

        private void ApplyDateFilter(List<Transaction> userTransactions)
        {
            FilteredTransactions = userTransactions
                .Where(t => t.Date.Date >= StartDate.Date && t.Date.Date <= EndDate.Date)
                .ToList();

            // Update inflows and outflows based on filtered data
            TotalInflows = FilteredTransactions
                .Where(t => t.Credit > 0)
                .Sum(t => t.Credit);

            TotalOutflows = FilteredTransactions
                .Where(t => t.Debit > 0)
                .Sum(t => t.Debit);
        }

        private void FilterDateRange()
        {
            var currentUser = UserService.GetCurrentUser();
            if (currentUser == null) return;

            var userTransactions = Data.Transactions?
                .Where(t => t.UserId == currentUser.Id)
                .ToList() ?? new List<Transaction>();

            ApplyDateFilter(userTransactions);
        }

        private async Task DownloadReceipt(Transaction transaction)
        {
            transaction.Type = transaction.Credit > 0 ? "Inflow" : "Outflow";

            UserService.GenerateReceipt(transaction);

            IsNotificationVisible = true;
            await Task.Delay(3000);
            IsNotificationVisible = false;

            StateHasChanged();
        }

        private void UpdateRemainingDebt(decimal remainingDebt)
        {
            RemainingDebt = remainingDebt;

            // Update NetBalance in UI after RemainingDebt changes
            NetBalance = UserService.NetBalance;

            StateHasChanged();
        }

        private void Logout()
        {
            UserService.Logout();
            NavigationManager.NavigateTo("/"); // Redirect to login page
        }

        private string FormatWithCurrency(decimal amount)
        {
            return SelectedCurrency switch
            {
                "USD" => $"${amount:F2}",
                "EUR" => $"€{amount:F2}",
                "INR" => $"₹{amount:F2}",
                "GBP" => $"£{amount:F2}",
                "JPY" => $"¥{amount:F2}",
                _ => amount.ToString("F2")
            };
        }

        public void Dispose()
        {
            UserService.OnNetBalanceChanged -= HandleNetBalanceChanged;
        }
    }
}
