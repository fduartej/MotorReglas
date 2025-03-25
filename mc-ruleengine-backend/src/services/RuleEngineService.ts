import fs from "fs/promises";
import path from "path";
import { StorageService } from "./StorageService";
import { ZenEngine } from "@gorules/zen-engine";

export class RuleEngineService {
  private storageService: StorageService;

  constructor() {
    this.storageService = new StorageService();
  }

  public async processRequest(
    decision: string,
    modelo: string,
    inputData: any
  ): Promise<any> {
    const localDirectory = process.env.APP_LOCAL_DIRECTORY;
    if (!localDirectory) {
      throw new Error(
        "APP_LOCAL_DIRECTORY is not defined in environment variables."
      );
    }

    const directoryPath = path.join(localDirectory, decision);

    try {
      // Verificar si el directorio existe
      await fs.access(directoryPath);
    } catch {
      // Crear el directorio si no existe
      console.log(`Directory does not exist. Creating: ${directoryPath}`);
      await fs.mkdir(directoryPath, { recursive: true });
    }

    // Define the local path and blob path
    const localPath = path.join(directoryPath, `${modelo}.json`);
    const blobPath = `${decision}/${modelo}.json`;

    // Check if the file already exists locally
    try {
      await fs.access(localPath);
      console.log(`File already exists locally: ${localPath}`);
    } catch {
      console.log(`File not found locally. Downloading: ${blobPath}`);
      await this.storageService.downloadBlob(blobPath, localPath);
    }

    // Process the file with ZenEngine
    return this.processFile(localPath, inputData);
  }

  public async processFile(filePath: string, inputData: any): Promise<any> {
    console.log(`Reading file from ${filePath}`);
    const content = await fs.readFile(filePath);
    console.log("File content read successfully.");

    console.log("Initializing ZenEngine...");
    const engine = new ZenEngine();
    const decision = engine.createDecision(content);

    console.log("Evaluating decision...");
    const result = await decision.evaluate(inputData);
    console.log("Decision evaluated successfully.");

    return result;
  }

  public async cleanLocalDirectory(): Promise<void> {
    const localDirectory = process.env.APP_LOCAL_DIRECTORY;
    if (!localDirectory) {
      throw new Error(
        "APP_LOCAL_DIRECTORY is not defined in environment variables."
      );
    }

    const directoryPath = path.resolve(localDirectory);

    try {
      // Verificar si el directorio existe
      await fs.access(directoryPath);
    } catch {
      // Crear el directorio si no existe
      console.log(`Directory does not exist. Creating: ${directoryPath}`);
      await fs.mkdir(directoryPath, { recursive: true });
    }

    // Leer y eliminar los archivos del directorio
    const files = await fs.readdir(directoryPath);
    for (const file of files) {
      const filePath = path.join(directoryPath, file);
      await fs.unlink(filePath);
      console.log(`Deleted file: ${filePath}`);
    }
    console.log("Local directory cleaned.");
  }
}
