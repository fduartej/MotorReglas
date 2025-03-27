# dotnet-backend-api

"Server=tcp:w6agsf2hbrquxbj5p5j5fvzkru-pjp3rzqv7qqela25nwuuxacbna.datawarehouse.fabric.microsoft.com,1433;" +
"Initial Catalog=silver;" +
"Authentication=ActiveDirectoryPassword;" +
"User ID=fduarte@calidda.com.pe;" +
"Password=TuPasswordAqui;" +
"Encrypt=True;TrustServerCertificate=False;";

## Overview

This project is a .NET 8 API designed to serve as a backend for various financial services, including underwriting and loan management. It is structured into modules, infrastructure, and integration components to promote separation of concerns and maintainability.

## Project Structure

- **Modules**: Contains the core business logic and controllers for different functionalities.

  - **Underwriting**: Manages underwriting processes.
    - **Controllers**: Handles HTTP requests related to underwriting.
    - **Mappers**: Maps entities to DTOs.
    - **Entities**: Defines the data structure for underwriting.
    - **Services**: Contains business logic for underwriting operations.
  - **Loan**: Manages loan processes.
    - **Controllers**: Handles HTTP requests related to loans.
    - **Mappers**: Maps entities to DTOs.
    - **Entities**: Defines the data structure for loans.
    - **Services**: Contains business logic for loan operations.

- **Infrastructure**: Provides data access and utility functions.

  - **Data**: Contains repositories for data access.
    - **Cosmos**: Interacts with Cosmos DB.
    - **Sql**: Interacts with SQL databases.
  - **Helpers**: Contains utility classes for resilience and other helper functions.
  - **Telemetry**: Configures monitoring and tracing using OpenTelemetry.

- **Integration**: Handles external integrations.
  - **GoRules**: Manages integration with GoRules for rule processing.

## Setup Instructions

1. **Clone the Repository**

   ```bash
   git clone https://github.com/yourusername/dotnet-backend-api.git
   cd dotnet-backend-api
   ```

2. **Install Dependencies**
   Ensure you have the .NET 8 SDK installed. Run the following command to restore the dependencies:

   ```bash
   dotnet restore
   ```

3. **Configuration**
   Update the `appsettings.json` file with your database connection strings and other necessary configurations.

4. **Run the Application**
   You can run the application using the following command:

   ```bash
   dotnet run
   ```

5. **Deploying to AKS**
   Follow the Azure Kubernetes Service (AKS) documentation to deploy the application. Ensure that your Dockerfile is set up correctly for containerization.

## Usage Guidelines

- The API endpoints for underwriting and loan management can be accessed via the respective controllers.
- Use the provided mappers to convert between entities and DTOs as needed.
- Implement resilience strategies using the PollyHelper class for external service calls.

## Contributing

Contributions are welcome! Please submit a pull request or open an issue for any enhancements or bug fixes.

## License

This project is licensed under the MIT License. See the LICENSE file for more details.
