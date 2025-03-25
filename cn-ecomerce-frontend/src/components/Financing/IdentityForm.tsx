import React, { useState } from "react";

interface IdentityFormProps {
  onSubmit: (data: {
    identityDocument: string;
    loanAmount: number;
    installments: number;
  }) => void;
  initialLoanAmount: number; // Monto inicial del carrito
}

const IdentityForm: React.FC<IdentityFormProps> = ({
  onSubmit,
  initialLoanAmount,
}) => {
  const [identityDocument, setIdentityDocument] = useState("");
  const [loanAmount, setLoanAmount] = useState(initialLoanAmount);
  const [installments, setInstallments] = useState(12); // Número de cuotas inicial

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit({ identityDocument, loanAmount, installments }); // Enviar los datos al padre
  };

  return (
    <form
      onSubmit={handleSubmit}
      style={{ maxWidth: "400px", margin: "0 auto" }}
    >
      <div style={{ marginBottom: "16px" }}>
        <label htmlFor="identityDocument">Documento de Identidad:</label>
        <input
          id="identityDocument"
          type="text"
          value={identityDocument}
          onChange={(e) => setIdentityDocument(e.target.value)}
          style={{ width: "100%", padding: "8px", marginTop: "4px" }}
          required
        />
      </div>
      <div style={{ marginBottom: "16px" }}>
        <label htmlFor="loanAmount">Monto a Financiar:</label>
        <input
          id="loanAmount"
          type="number"
          value={loanAmount}
          onChange={(e) => setLoanAmount(Number(e.target.value))}
          style={{ width: "100%", padding: "8px", marginTop: "4px" }}
          required
          min={1}
        />
      </div>
      <div style={{ marginBottom: "16px" }}>
        <label htmlFor="installments">Numero de Cuotas:</label>
        <input
          id="installments"
          type="number"
          value={installments}
          onChange={(e) => {
            const value = Math.max(12, Number(e.target.value)); // Asegurar que el valor mínimo sea 12
            setInstallments(value);
          }}
          style={{ width: "100%", padding: "8px", marginTop: "4px" }}
          required
          min={12}
        />
      </div>
      <button
        type="submit"
        style={{
          padding: "12px 24px",
          backgroundColor: "#0070f3",
          color: "#fff",
          border: "none",
          borderRadius: "4px",
          cursor: "pointer",
        }}
      >
        Evaluar
      </button>
    </form>
  );
};

export default IdentityForm;
