# Maintenance Work Order Web Portal

A web-based application for managing maintenance work orders and facility equipment, developed as a term project for Web Development and Database courses.

## Overview

This portal allows users to create, track, and manage maintenance work orders for facility equipment. The system includes role-based access control with different user types (Admin, Manager, Technician) and real-time updates using SignalR. 

## Features

- **Work Order Management**: Create, update, and track maintenance work orders
- **Equipment Management**:  Maintain records of facility equipment
- **Spare Parts Tracking**:  Manage spare parts inventory
- **User Roles**: Admin, Manager, and Technician roles with different permissions
- **Real-time Updates**: Live notifications using SignalR
- **File Attachments**: Upload and manage work order attachments
- **Work Order History**: Track changes and status updates

## Technologies Used

- **Backend**: ASP.NET Core (. NET 10.0)
- **Database**: SQL Server with Entity Framework Core
- **Frontend**:  HTML, CSS, JavaScript
- **Authentication**: ASP.NET Core Identity
- **Real-time Communication**: SignalR

## Setup Instructions

1. Clone the repository
2. Open the solution in Visual Studio
3. Update the connection string in `appsettings.json` if needed
4. Run database migrations:
   ```
   Update-Database
   ```
5. Run the application
6. The system will automatically seed an admin user on first run

## Project Structure

- `Models/`: Data models and entities
- `Services/`: Business logic layer
- `Controllers/`: MVC controllers
- `Views/`: Razor views
- `Data/`: Database context and migrations
- `Hubs/`: SignalR hubs for real-time communication

## Default Credentials

Admin credentials are created automatically on first run (check DbInitializer.cs for details).

## Course Information

This project was developed as a term project for: 
- Web Development course
- Database course

---

*Academic Project - 2025*
