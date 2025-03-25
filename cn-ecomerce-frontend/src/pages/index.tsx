import { useEffect, useState } from "react";
import ProductList from "../components/Catalog/ProductList";
import { fetchCatalog } from "../services/api";

const Home = () => {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const loadCatalog = async () => {
      try {
        await fetchCatalog(); // Si necesitas manejar los datos, puedes almacenarlos en un estado
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

    loadCatalog();
  }, []);

  if (loading) return <div>Cargando...</div>;
  if (error) return <div>Error: {error}</div>;

  return (
    <div>
      <h1>Catalogo de Productos</h1>
      <ProductList />
    </div>
  );
};

export default Home;
