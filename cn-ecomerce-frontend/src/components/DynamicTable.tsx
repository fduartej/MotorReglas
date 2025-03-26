import React from "react";

interface DynamicTableProps {
  data: any; // Los datos a renderizar
  title?: string; // Título opcional para la tabla
}

const DynamicTable: React.FC<DynamicTableProps> = ({ data, title }) => {
  if (!data || typeof data !== "object") {
    return null; // Si no hay datos o no es un objeto, no renderizar nada
  }

  return (
    <div style={{ marginBottom: "16px" }}>
      {title && <h3>{title}</h3>}
      <table
        style={{
          width: "100%",
          borderCollapse: "collapse",
          marginBottom: "16px",
        }}
      >
        <tbody>
          {Object.entries(data).map(([key, value]) => (
            <tr key={key} style={{ borderBottom: "1px solid #ddd" }}>
              <td
                style={{
                  padding: "8px",
                  fontWeight: "bold",
                  textAlign: "left",
                  borderRight: "1px solid #ddd",
                }}
              >
                {key}
              </td>
              <td
                style={{
                  padding: "8px",
                  textAlign: "left",
                  color:
                    value === true
                      ? "blue"
                      : value === false
                      ? "red"
                      : "inherit", // Estilo condicional
                }}
              >
                {value === null ? (
                  "-" // Mostrar "-" si el valor es null
                ) : typeof value === "object" && value !== null ? (
                  <DynamicTable data={value} /> // Renderizar recursivamente si el valor es un objeto
                ) : (
                  String(value) // Mostrar el valor como texto
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
};

export default DynamicTable;
