import axios from "axios";

const API_BASE_URL = process.env.API_BASE_URL || "http://localhost:5000";

// Función para obtener el catálogo de productos
export const fetchCatalog = async () => {
  try {
    const response = await axios.get(`${API_BASE_URL}/ecomerce/catalogo`);
    return response.data;
  } catch (error) {
    console.error("Error fetching catalog:", error);
    throw error;
  }
};

// Función para obtener un producto específico (si es necesario)
export const fetchProductById = async (productId: string | number) => {
  try {
    const response = await axios.get(
      `${API_BASE_URL}/ecomerce/catalogo/${productId}`
    );
    return response.data;
  } catch (error) {
    console.error(`Error fetching product with ID ${productId}:`, error);
    throw error;
  }
};

// Función para manejar otras solicitudes relacionadas con el carrito o financiamiento (si es necesario)
export const submitFinancingRequest = async (
  docId: string,
  data: { LoanAmountRequested: number; LoanTerm: number }
) => {
  try {
    const response = await axios.post(
      `${API_BASE_URL}/loan/${docId}/evaluation`,
      data
    );
    return response.data;
  } catch (error) {
    console.error("Error submitting financing request:", error);
    throw error;
  }
};
