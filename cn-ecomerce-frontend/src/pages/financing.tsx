import React, { useState } from "react";
import IdentityForm from "../components/Financing/IdentityForm";
import { useCart } from "../context/CartContext";
import { calculateTotal } from "../utils/calculateTotal";
import { submitFinancingRequest } from "../services/api";
import DynamicTable from "../components/DynamicTable";

const FinancingPage: React.FC = () => {
  const { cartItems } = useCart();
  const [submitted, setSubmitted] = useState(false);
  const [response, setResponse] = useState<any | null>(null); // Guardar la respuesta completa
  const [error, setError] = useState<string | null>(null);

  const totalAmount = calculateTotal(cartItems); // Calcular el monto total del carrito

  const handleFormSubmit = async (data: {
    identityDocument: string;
    loanAmount: number;
    installments: number;
  }) => {
    try {
      const result = await submitFinancingRequest(data.identityDocument, {
        LoanAmountRequested: data.loanAmount,
        LoanTerm: data.installments,
      });
      setResponse(result); // Guardar la respuesta completa del servicio
      setSubmitted(true);
    } catch (err) {
      setError("Failed to submit financing request. Please try again.");
      console.error(err);
    }
  };

  return (
    <div style={{ padding: "16px" }}>
      <h1>FNB - Financiamiento</h1>
      {submitted && response ? (
        <div>
          <h2>Resultado de la Evaluación</h2>
          {response.evaluation && (
            <>
              <DynamicTable
                data={response.evaluation.result.creditDecision}
                title="Credit Decision"
              />
              <DynamicTable
                data={response.evaluation.result.validations}
                title="Validations"
              />
            </>
          )}
        </div>
      ) : (
        <>
          {error && <p style={{ color: "red" }}>{error}</p>}
          <IdentityForm
            onSubmit={handleFormSubmit}
            initialLoanAmount={totalAmount}
          />
        </>
      )}
    </div>
  );
};

export default FinancingPage;
