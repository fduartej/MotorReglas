interface CartItem {
  id: string;
  name: string;
  price: number;
  quantity: number;
}

export function calculateTotal(cartItems: CartItem[]): number {
  return cartItems.reduce(
    (total, item) => total + item.price * item.quantity,
    0
  );
}
