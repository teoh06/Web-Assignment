================================================================================
                        WEB APPLICATION 2 - FOOD ORDERING SYSTEM
                                    README FILE
================================================================================

OVERVIEW
--------
This is a web-based food ordering system built with ASP.NET Core MVC. The system 
supports multiple user roles including Administrators and Members (customers).

SYSTEM REQUIREMENTS
-------------------
- .NET 6.0 or later
- SQL Server or SQL Server Express
- Visual Studio 2022 or Visual Studio Code
- Entity Framework Core

SETUP INSTRUCTIONS
------------------
1. Clone or download the project to your local machine
2. Open the solution file (WebApplication2.sln) in Visual Studio
3. Go to View > SQL Server Object Explorer > SQL Server > (localdb)\MSSQLLocalDB > Databases
4. Check if the database is created named 'DB'. 
   If yes, delete it and recreate the database named 'DB' within the solution.
   If no, direct create the database by right click Databases > Add New Database > 
   Database Name: DB
   Database Location: [Your solution file path] eg. C:\Users\H.Bing\source\repos\Web-Assignment\WebApplication2
5. Restore NuGet packages
6. Update the connection string in appsettings.json if needed
7. Run the application - the database will be created automatically with seed data
8. Navigate to the application URL (typically https://localhost:7xxx)

================================================================================
                              LOGIN CREDENTIALS
================================================================================

ADMINISTRATOR ACCOUNTS
----------------------
Use these credentials to access the admin panel and manage the system:

Email: admin@gmail.com
Password: 123456
Role: Administrator
Description: Main system administrator with full access to all features

MEMBER ACCOUNTS (CUSTOMERS)
---------------------------
Use these credentials to test the customer ordering functionality:

Email: yaphb-wm24@student.tarc.edu.my
Password: 123456
Name: TARC Student
Address: TARC University College, Setapak
Phone: 0123456789

================================================================================
                              SYSTEM FEATURES
================================================================================

ADMINISTRATOR FEATURES
----------------------
- User management (view, edit, delete users)
- Order management (view all orders, update order status)
- Menu item management (add, edit, delete menu items)
- Category management (create, edit, delete categories)
- Sales reporting and analytics
- System configuration

MEMBER FEATURES
---------------
- User registration and profile management
- Browse menu items by category
- Add items to cart with customization options
- Place orders with delivery options
- View order history
- Rate and comment on menu items
- Add items to favorites/wishlist
- Account management (update profile, change password)

MENU CATEGORIES
---------------
The system comes pre-loaded with the following categories:
- Western Food (burgers, pizza, fish & chips)
- Salads (Caesar salad, etc.)
- Desserts (tiramisu, pudding)
- Beverages (Coca-Cola, iced latte)

TESTING SCENARIOS
-----------------
1. Admin Testing:
   - Login as admin@gmail.com
   - Navigate to Admin panel
   - Test order management, user management, and menu management

2. Customer Testing:
   - Login as any member account
   - Browse menu items
   - Add items to cart
   - Place test orders
   - Test profile management features

3. Registration Testing:
   - Create new member accounts
   - Test email validation and password requirements

PAYMENT METHODS
---------------
The system supports the following payment methods (Simulated):
- Cash on Delivery
- Credit/Debit Card

DELIVERY OPTIONS
----------------
- Pickup
- Delivery (with address specification)

TROUBLESHOOTING
---------------
1. If login fails, ensure the database has been seeded with the default accounts
2. If the application doesn't start, check the connection string in appsettings.json
3. For database issues, delete the existing database and restart the application
4. Ensure all NuGet packages are properly restored

SUPPORT
-------
For technical support or questions about the system, please contact the development team.

================================================================================
                                 NOTES
================================================================================

- All passwords are hashed using secure hashing algorithms
- The system includes protection against duplicate orders
- Email functionality may require SMTP configuration
- The system supports file uploads for menu item images and user profile photos
- Stock management is implemented for menu items
- The application includes responsive design for mobile devices

Last Updated: September 17, 2025
