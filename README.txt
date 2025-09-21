================================================================================
                        WEB APPLICATION 2 - FOOD ORDERING SYSTEM
                                    README FILE
================================================================================

OVERVIEW
--------
This is a web-based food ordering system built with ASP.NET Core MVC. The system 
supports multiple user roles including Administrators, Chefs, and Members (customers).
Features advanced order management, real-time search, payment processing, and 
comprehensive order tracking with role-based access control.

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
Description: Main system administrator with full access to all features including
            user management, order management, refunds, and system configuration

CHEF ACCOUNTS
-------------
Use these credentials to access the chef panel for kitchen operations:

Email: chef@gmail.com
Password: 123456
Role: Chef
Description: Kitchen staff with access to order status management for food preparation.
            Can modify orders between Pending, Paid, Preparing, Ready for Pickup, and 
            Delivered statuses. Cannot handle refunds or declines (Admin only).

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
- Complete order management (view all orders, update any order status)
- Menu item management (add, edit, delete menu items with soft delete)
- Category management (create, edit, delete categories)
- Sales reporting and analytics
- System configuration
- Handle refunds and order declines
- Restore deleted menu items
- Advanced order search and filtering
- Order status modification with audit trail (up to 2 modifications per order)

CHEF FEATURES
-------------
- Kitchen-focused order management dashboard
- Update order status for food preparation workflow:
  * Pending → Paid → Preparing → Ready for Pickup → Delivered
- View order details and special instructions
- Track order preparation progress
- Role-restricted access (cannot handle refunds/declines)
- One modification per order limit for kitchen operations

MEMBER FEATURES
---------------
- User registration and profile management
- Browse menu items by category with advanced search
- Add items to cart with customization options
- Place orders with delivery options (Pickup/Delivery)
- Multiple payment methods (Cash/Credit Card) with real-time validation
- Comprehensive order history with search functionality
- Order tracking with real-time status updates
- Self-service order cancellation and refund requests
- Complete payment for pending orders from multiple entry points
- Rate and comment on menu items
- Add items to favorites/wishlist
- Account management (update profile, change password)
- Advanced order search by ID, items, dates, status, and delivery info

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
   - Test complete order management (including refunds and declines)
   - Test user management and menu management with soft delete
   - Test order status modifications (up to 2 per order)
   - Test advanced search and filtering features

2. Chef Testing:
   - Login as chef@gmail.com
   - Navigate to Chef dashboard
   - Test kitchen workflow order status updates
   - Verify role restrictions (no access to refunds/declines)
   - Test one-time modification limit per order

3. Customer Testing:
   - Login as any member account
   - Browse menu items with search functionality
   - Add items to cart and test payment validation
   - Place test orders with different delivery options
   - Test order tracking and history search
   - Test self-service refund requests
   - Test "Complete Payment" functionality for pending orders
   - Test profile management features

4. Registration Testing:
   - Create new member accounts
   - Test email validation and password requirements

5. Payment Testing:
   - Test credit card validation with expiry date checks
   - Test billing address optional functionality
   - Test real-time form validation with button state management
   - Test payment completion from multiple entry points

6. Order Management Testing:
   - Test order status modification limits (2 total, 1 per role)
   - Test soft delete for menu items (preserves order history)
   - Test advanced search across all order fields
   - Test refund animations and user experience

PAYMENT METHODS
---------------
The system supports the following payment methods with advanced validation:
- Cash on Delivery
- Credit/Debit Card with real-time validation:
  * Card number validation (16 digits)
  * Expiry date validation (MM/YY format, not expired, future limit)
  * CVV validation (3-4 digits)
  * Cardholder name validation
  * Billing address (optional)
  * Dynamic form validation with button state management

DELIVERY OPTIONS
----------------
- Pickup (customer collects from restaurant)
- Delivery (with address specification and phone number requirement)

TROUBLESHOOTING
---------------
1. If login fails, ensure the database has been seeded with the default accounts
2. If the application doesn't start, check the connection string in appsettings.json
3. For database issues, delete the existing database and restart the application
4. Ensure all NuGet packages are properly restored
5. If Chef role features are not working, verify the user is assigned the "Chef" role
6. If payment validation issues occur, check browser console for JavaScript errors
7. If order search is not working, ensure AJAX endpoints are properly configured
8. For order modification issues, check role permissions and modification count limits

SUPPORT
-------
For technical support or questions about the system, please contact the development team.

================================================================================
                                 NOTES
================================================================================

SECURITY & DATA INTEGRITY
--------------------------
- All passwords are hashed using secure hashing algorithms
- Role-based access control with strict permission enforcement
- Soft delete implementation preserves order history integrity
- Protection against duplicate orders and invalid submissions
- Real-time form validation prevents invalid data entry

ADVANCED FEATURES
-----------------
- AJAX-powered search system with real-time results
- Dynamic form validation with visual feedback
- Animated user interactions (refund animations, button states)
- Comprehensive order tracking and status management
- Multi-role workflow with modification limits and audit trails
- Advanced payment validation with expiry date checking

TECHNICAL IMPLEMENTATION
------------------------
- Entity Framework Core with complex relationships
- Server-side and client-side validation
- Responsive design optimized for mobile devices
- File upload support for menu item images and user profile photos
- Stock management with automatic inventory updates
- Email functionality (may require SMTP configuration)
- Bootstrap-based UI with custom animations and styling

BUSINESS LOGIC
--------------
- Order status modification limits (2 total, 1 per role)
- Role-specific access restrictions (Chef vs Admin capabilities)
- Automatic order cleanup for refunded/declined orders
- Inventory restocking on refunds and cancellations
- Comprehensive search across multiple order fields
- Real-time payment completion from multiple entry points

Last Updated: September 21, 2025
