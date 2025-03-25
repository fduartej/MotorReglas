// filepath: d:\Root\Code\calidda\MotorReglas\cn-ecomerce-frontend\src\components\Catalog\ProductList.tsx
import React, { useEffect, useState } from "react";
import ProductCard from "./ProductCard";
import { fetchCatalog } from "../../services/api";
import { useCart } from "../../context/CartContext";

interface Product {
  id: string;
  name: string;
  description: string;
  price: number;
  image: string;
  category: string;
}

const ProductList: React.FC = () => {
  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const { addToCart } = useCart(); // Usar la función addToCart del contexto

  useEffect(() => {
    const loadProducts = async () => {
      try {
        const data = await fetchCatalog();
        setProducts(data);
      } catch (err) {
        if (err instanceof Error) {
          setError(err.message);
        } else {
          setError("An unknown error occurred");
        }
      } finally {
        setLoading(false);
      }
    };

    loadProducts();
  }, []);

  if (loading) return <div>Cargando...</div>;
  if (error) return <div>Error: {error}</div>;

  return (
    <div>
      {products.map((product) => (
        <ProductCard
          key={product.id}
          product={product}
          onAddToCart={() => addToCart({ ...product, quantity: 1 })} // Pasar la función addToCart
        />
      ))}
    </div>
  );
};

export default ProductList;
