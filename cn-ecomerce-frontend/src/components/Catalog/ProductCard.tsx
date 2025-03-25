import React from "react";
import { useCart } from "../../context/CartContext";

interface Product {
  id: string;
  name: string;
  description: string;
  price: number;
  image: string;
}

interface ProductCardProps {
  product: Product;
  onAddToCart: () => void; // Agregar la prop onAddToCart
}

const ProductCard: React.FC<ProductCardProps> = ({ product, onAddToCart }) => {
  return (
    <div className="product-card">
      <img src={product.image} alt={product.name} />
      <h3>{product.name}</h3>
      <p>{product.description}</p>
      <p>${product.price.toFixed(2)}</p>
      <button onClick={onAddToCart}>Agregar al Carrito</button>
    </div>
  );
};

export default ProductCard;
