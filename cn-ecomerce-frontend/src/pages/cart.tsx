import React from "react";
import { useCart } from "../context/CartContext";
import CartSummary from "../components/Cart/CartSummary";
import CartItem from "../components/Cart/CartItem";
import { calculateTotal } from "../utils/calculateTotal";
import { useRouter } from "next/router";

const Cart: React.FC = () => {
  const { cartItems, setCartItems } = useCart();
  const router = useRouter(); // Para redirigir a la página de financiamiento

  const handleRemoveItem = (id: string) => {
    const updatedCart = cartItems.filter((item) => item.id !== id);
    setCartItems(updatedCart);
    localStorage.setItem("cart", JSON.stringify(updatedCart));
  };

  const totalAmount = calculateTotal(cartItems);

  const handleFinancing = () => {
    router.push("/financing"); // Redirigir a la página de financiamiento
  };

  return (
    <div style={{ padding: "16px" }}>
      <h1>Lista de compras</h1>
      {cartItems.length === 0 ? (
        <p>El carrito esta vacio.</p>
      ) : (
        <div>
          {cartItems.map((item) => (
            <CartItem key={item.id} item={item} onRemove={handleRemoveItem} />
          ))}
          <CartSummary totalAmount={totalAmount} />
          <button
            onClick={handleFinancing}
            style={{
              marginTop: "16px",
              padding: "12px 24px",
              backgroundColor: "#0070f3",
              color: "#fff",
              border: "none",
              borderRadius: "4px",
              cursor: "pointer",
            }}
          >
            Financiar
          </button>
        </div>
      )}
    </div>
  );
};

export default Cart;
