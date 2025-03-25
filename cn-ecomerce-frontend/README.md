# Shopping Cart Application

This is a shopping cart application built with Next.js. It allows users to browse a catalog of products, add items to their cart, view their cart, and proceed to financing by submitting their identity document.

## Features

- **Product Catalog**: Users can view a list of products fetched from a remote API.
- **Product View**: Each product can be viewed in detail, including its name, description, and price.
- **Add to Cart**: Users can add products to their shopping cart.
- **View Cart**: Users can view the items they have added to their cart along with the total amount.
- **Financing**: Users can proceed to financing where they are prompted to enter their identity document for loan processing.

## Project Structure

```
shopping-cart-app
├── public
├── src
│   ├── components
│   │   ├── Cart
│   │   │   ├── CartItem.tsx
│   │   │   └── CartSummary.tsx
│   │   ├── Catalog
│   │   │   ├── ProductCard.tsx
│   │   │   └── ProductList.tsx
│   │   └── Financing
│   │       └── IdentityForm.tsx
│   ├── pages
│   │   ├── api
│   │   │   └── catalog.ts
│   │   ├── _app.tsx
│   │   ├── index.tsx
│   │   ├── cart.tsx
│   │   └── financing.tsx
│   ├── services
│   │   └── api.ts
│   ├── styles
│   │   ├── globals.css
│   │   └── Cart.module.css
│   └── utils
│       └── calculateTotal.ts
├── package.json
├── tsconfig.json
└── README.md
```

## Getting Started

1. Clone the repository:
   ```
   git clone <repository-url>
   ```
2. Navigate to the project directory:
   ```
   cd shopping-cart-app
   ```
3. Install dependencies:
   ```
   npm install
   ```
4. Start the development server:
   ```
   npm run dev
   ```
5. Open your browser and go to `http://localhost:3000` to view the application.

## API

The application fetches the product catalog from the following endpoint:
```
GET http://localhost:5000/ecomerce/catalogo
```

## Contributing

Feel free to submit issues or pull requests for any improvements or bug fixes.

## License

This project is licensed under the MIT License.