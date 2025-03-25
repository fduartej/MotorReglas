import express from "express";
import bodyParser from "body-parser";
import { config } from "dotenv";
import { IndexController } from "./controllers/IndexController";

// Load environment variables
config();

// Validate required environment variables
const requiredEnvVars = [
  "AZURE_STORAGE_CONNECTION_STRING",
  "AZURE_STORAGE_CONTAINER_NAME",
  "APP_LOCAL_DIRECTORY",
];
requiredEnvVars.forEach((envVar) => {
  if (!process.env[envVar]) {
    throw new Error(`Environment variable ${envVar} is not defined.`);
  }
});

const app = express();
const port = process.env.PORT || 3000;

app.use(bodyParser.json());

const indexController = new IndexController();

app.get("/", (req, res) => {
  res.send("Welcome to the MC Rule Engine Backend!");
});

app.post("/engine/execute", (req, res) =>
  indexController.handleRequest(req, res)
);
app.delete("/engine/clean", (req, res) =>
  indexController.cleanLocalFiles(req, res)
);

app.listen(port, () => {
  console.log(`Server is running on http://localhost:${port}`);
});
