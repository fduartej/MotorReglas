import React from "react";
import styles from "../../styles/Cart.module.css";
import { calculateTotal } from "../../utils/calculateTotal";

interface CartSummaryProps {
  totalAmount: number; // Define la prop totalAmount
}

const CartSummary: React.FC<CartSummaryProps> = ({ totalAmount }) => {
  return (
    <div className={styles.cartSummary}>
      <h2>Resumen del Carrito</h2>
      <p>Monto Total: S/.{totalAmount.toFixed(2)}</p>
    </div>
  );
};

export default CartSummary;
