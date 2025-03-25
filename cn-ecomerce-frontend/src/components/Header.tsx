import React from "react";
import Link from "next/link";
import { useCart } from "../context/CartContext";

const Header: React.FC = () => {
  const { cartItems } = useCart();

  // Calcular la cantidad total de productos en el carrito
  const totalItems = cartItems.reduce((sum, item) => sum + item.quantity, 0);

  return (
    <header style={styles.header}>
      <Link href="/">
        <a style={styles.logo}>Tienda Virtual</a>
      </Link>
      <Link href="/cart">
        <a style={styles.cartButton}>🛒 Carrito ({totalItems})</a>
      </Link>
    </header>
  );
};

const styles = {
  header: {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
    padding: "16px",
    backgroundColor: "#0070f3",
    color: "#fff",
  },
  logo: {
    fontSize: "1.5rem",
    fontWeight: "bold",
    textDecoration: "none",
    color: "#fff",
  },
  cartButton: {
    fontSize: "1rem",
    textDecoration: "none",
    color: "#fff",
    backgroundColor: "#005bb5",
    padding: "8px 16px",
    borderRadius: "4px",
  },
};

export default Header;
