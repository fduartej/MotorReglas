import React from "react";

interface CartItemProps {
  item: {
    id: string;
    name: string;
    price: number;
    quantity: number;
  };
  onRemove: (id: string) => void; // Prop para manejar la eliminación del producto
}

const CartItem: React.FC<CartItemProps> = ({ item, onRemove }) => {
  return (
    <div className="cart-item">
      <h3>{item.name}</h3>
      <p>Precio: S/.{item.price}</p>
      <p>Cantidad: {item.quantity}</p>
      <p>Total: S/.{item.price * item.quantity}</p>
      <button onClick={() => onRemove(item.id)}>Remove</button>
    </div>
  );
};

export default CartItem;
