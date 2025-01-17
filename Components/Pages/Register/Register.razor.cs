using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Ing_ExpenseTracker.Models;
namespace Ing_ExpenseTracker.Components.Pages.Register
{
    public partial class Register : ComponentBase
    {
        private string RegisterUsername = "";
        private string RegisterPassword = "";
        private string ConfirmPassword = "";
        private string RegisterEmail = "";
        private string PreferredCurrency = "";
        private string Message = "";
        private AppData data;

        protected override void OnInitialized()
        {
            data = UserService.LoadUsers();
        }

        private void RegisterPage()
        {
            var data = UserService.LoadUsers();

            // Check if required fields are filled
            if (string.IsNullOrWhiteSpace(RegisterUsername) || string.IsNullOrWhiteSpace(RegisterPassword))
            {
                Message = "All fields are required.";
                return;
            }

            // Check if passwords match
            if (RegisterPassword != ConfirmPassword)
            {
                Message = "Passwords do not match.";
                return;
            }

            // Check if username already exists
            if (data.Users.Any(u => u.Username.Equals(RegisterUsername, StringComparison.OrdinalIgnoreCase)))
            {
                Message = "Username already exists.";
                return;
            }

            // Create new user
            var newUser = new User
            {
                Id = data.Users.Any() ? data.Users.Max(u => u.Id) + 1 : 1, // Unique UserId
                Username = RegisterUsername,
                Password = UserService.HashPassword(RegisterPassword), // Hash password for security
                Email = RegisterEmail,
                CreatedOn = DateTime.Now,
                PreferredCurrency = PreferredCurrency // Save the selected currency
            };

            data.Users.Add(newUser);
            UserService.SaveUsers(data);

            // Set the new user as the current user
            UserService.SetCurrentUser(newUser);

            // Redirect to dashboard
            NavigationManager.NavigateTo("/");
        }

        private void NavigateToLogin()
        {
            NavigationManager.NavigateTo("/");
        }
    }

}
