import type { NextApiRequest, NextApiResponse } from "next";
import { fetchCatalog } from "../../services/api";

export default async function handler(
  req: NextApiRequest,
  res: NextApiResponse
) {
  if (req.method === "GET") {
    try {
      const data = await fetchCatalog();
      res.status(200).json(data);
    } catch (error) {
      console.error("Error fetching catalog:", error);
      res.status(500).json({ message: "Error fetching catalog" });
    }
  } else {
    res.setHeader("Allow", ["GET"]);
    res.status(405).end(`Method ${req.method} Not Allowed`);
  }
}
