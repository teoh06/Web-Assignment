# 🍽️ Menu & Food Ordering System

![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-9.0+-512BD4?logo=dotnet)
![C#](https://img.shields.io/badge/C%23-239120?logo=c-sharp&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL_Server-CC2927?logo=microsoft-sql-server&logoColor=white)
![Bootstrap](https://img.shields.io/badge/Bootstrap-7952B3?logo=bootstrap&logoColor=white)
![Entity Framework](https://img.shields.io/badge/Entity_Framework_Core-512BD4?logo=entity-framework)

## 📌 Project Overview

This **Menu and Food Ordering System** is a comprehensive web application developed for the **AMIT2014 Web and Mobile Systems** course assignment. The system is designed to streamline in-house dining experiences for restaurants and canteens, providing an intuitive interface for customers to browse menus, place orders, and make payments, while offering restaurant staff efficient tools for order management and reporting.

Built with modern web technologies, this application demonstrates mastery of ASP.NET MVC architecture, Entity Framework, and SQL Server Express database implementation, while incorporating advanced features that enhance user experience and system functionality.

## ✨ Key Features

### 🍴 Core Modules

- **User Authentication System**
  - Role-based access control (User, Admin, Member)
  - Secure login/logout functionality
  - Password reset with token-based email verification
  
- **Menu Management**
  - Categorized menu items (eg. Appetizers, Main Courses, Desserts, Beverages)
  - Dynamic filtering by category, price range, and dietary preferences
  - Image gallery for each menu item
  
- **Order Processing System**
  - Shopping cart functionality with real-time updates
  - Table selection and reservation system
  - Order customization (special requests, modifications)
  - Multiple payment options (cash, card, e-wallet)
  
- **Order Management Dashboard**
  - Real-time order tracking of their status
  - Table management interface

### 🚀 Additional Features
  
- **Interactive Menu Customization**
  - Ratings and comments interaction (On development)
  - Visual preview of customized dishes
  
- **Advanced Reporting System**
  - Sales analytics with interactive charts
  - Revenue reports by time period
  - Simple PDF download for revenue report
  
- **Multi-language Support** (On development)
  - English and Chinese language options
  - Seamless language switching
  
- **Accessibility Features**
- Coming Soon

## 🛠️ Technical Implementation

### Architecture
- Strictly follows **MVC architecture** pattern
- Clean separation of concerns between presentation, business logic, and data layers
- Well-organized folder structure for maintainability

### Technologies Stack
| Layer | Technologies |
|-------|--------------|
| **Frontend** | HTML5, CSS3, Bootstrap 5, jQuery, JavaScript, AJAX |
| **Backend** | ASP.NET Core MVC 9.0, C# |
| **Data Access** | Entity Framework Core, SQL Server Express |
| **Additional** | SignalR (for real-time features), Chart.js, i18n |

### Security Implementation
- Role-based authorization for all protected resources
- Password hashing with salt
- CSRF protection for all forms
- Input validation (both client-side and server-side)
- Temporary login blocking after 3 failed attempts

## 📦 Installation & Setup

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [SQL Server Express](https://www.microsoft.com/en-us/sql-server/sql-server-editions-express)
- Visual Studio 2022 (recommended)

### Step-by-Step Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/teoh06/Web-Assignment.git
   cd menu-ordering-system
   ```

2. **Configure the database connection:**
   - Open `appsettings.json`
   - Update the `ConnectionStrings` section with your SQL Server details:
     ```json
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost;Database=DB;Trusted_Connection=True;MultipleActiveResultSets=true"
     }
     ```

3. **Apply database migrations:**
   ```bash
   dotnet ef database update
   ```

4. **Seed initial data (optional):**
   ```bash
   dotnet run --seed-data
   ```

5. **Run the application:**
   ```bash
   dotnet run
   ```
   Or use Visual Studio to build and run the solution.

6. **Access the application:**
   - Navigate to `https://localhost:----` in your browser

### 🔑 Test Credentials

| Role | Username | Password |
|------|----------|----------|
| Manager | manager@restaurant.com | Passw0rd! |
| Staff | staff@restaurant.com | Passw0rd! |
| Customer | customer@restaurant.com | Passw0rd! |

## 🗂️ Project Structure

```
MenuOrderingSystem/
├── Controllers/            # MVC controllers
├── Models/                 # Entity models and view models
├── Views/                  # Razor views organized by controller
├── Data/                   # DbContext and database configuration
├── wwwroot/                # Static assets
│   ├── css/                # Custom stylesheets
│   ├── js/                 # JavaScript files
│   └── img/                # Images and icons
├── Services/               # Business logic services
├── Utilities/              # Helper classes and extensions
├── Migrations/             # Database migration history
├── appsettings.json        # Configuration settings
├── Program.cs              # Application startup configuration
└── MenuOrderingSystem.csproj # Project file
```

## 📷 Screenshots

### Customer Interface
![Customer Menu View](screenshots/customer-menu.png) (On development)
*Browse menu items with detailed descriptions and images*

![Customer Cart View](screenshots/customer-cart.png) (On development)
*Interactive shopping cart with real-time updates*

### Staff Interface
![Staff Order Management](screenshots/staff-orders.png) (On development)
*Real-time order tracking and management dashboard*

### Manager Interface
![Manager Reporting](screenshots/manager-reports.png) (On development)
*Interactive sales analytics and reporting*

## 📊 Technical Highlights

### Entity Class Diagram
![Entity Class Diagram](screenshots/entity-diagram.png) (On development)

### Stepwise Refinement Approach
Our development followed a systematic stepwise refinement approach:
1. Identified core business processes
2. Broke down into manageable modules
3. Implemented foundational architecture
4. Added core functionality
5. Enhanced with additional features
6. Refined user experience

### Monetization Strategy
Our proposed monetization models for this system include:
- **Subscription Model**: Monthly/annual fees based on restaurant size
- **Transaction Fee**: Small percentage on each order processed
- **Premium Features**: Advanced analytics and integrations as add-ons
- **Customization Services**: Professional setup and customization services

## 📜 Academic Integrity Statement

> We declare that this assignment is our own work except where due acknowledgment is made. We have followed TAR UMT's Plagiarism Policy and confirm that this work is original. All group members have contributed substantially to the development of this application. The code has been developed using ASP.NET MVC Core, Entity Framework, and SQL Server Express as required by the AMIT2014 Web and Mobile Systems course guidelines.

## 📅 Submission Information
- **Course**: AMIT2014 Web and Mobile Systems
- **Submission Deadline**: Week 13-14
- **Project Type**: Menu and Ordering System

*This project was developed as part of the AMIT2014 Web and Mobile Systems course requirements.*
