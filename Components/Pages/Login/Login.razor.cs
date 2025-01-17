using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ing_ExpenseTracker.Models;
using Microsoft.AspNetCore.Components;


namespace Ing_ExpenseTracker.Components.Pages.Login
{
    public partial class Login : ComponentBase
    {
        private string LoginUsername = "";
        private string LoginPassword = "";
        private string Message = "";
        private AppData data;

        // private List<User> Users = new();

        protected override void OnInitialized()
        {
            data = UserService.LoadUsers();
        }

        private void LoginPage()
        {
            var data = UserService.LoadUsers();

            // Find the user by username
            var user = data.Users.FirstOrDefault(u => u.Username.Equals(LoginUsername, StringComparison.OrdinalIgnoreCase));

            // Validate the password
            if (user != null && UserService.ValidatePassword(LoginPassword, user.Password))
            {
                // Set the logged-in user as the current user
                UserService.SetCurrentUser(user);

                // Redirect to dashboard
                NavigationManager.NavigateTo("/dashboard");
            }
            else
            {
                Message = "Invalid username or password.";
            }
        }

    }
}
