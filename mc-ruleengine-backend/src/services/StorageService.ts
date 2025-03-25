import { BlobServiceClient } from "@azure/storage-blob";

export class StorageService {
  private blobServiceClient: BlobServiceClient;

  constructor() {
    const connectionString = process.env.AZURE_STORAGE_CONNECTION_STRING;
    if (!connectionString) {
      throw new Error(
        "AZURE_STORAGE_CONNECTION_STRING is not defined in environment variables."
      );
    }
    this.blobServiceClient =
      BlobServiceClient.fromConnectionString(connectionString);
  }

  public async downloadBlob(
    blobName: string,
    downloadPath: string
  ): Promise<void> {
    const containerName = process.env.AZURE_STORAGE_CONTAINER_NAME;
    if (!containerName) {
      throw new Error(
        "AZURE_STORAGE_CONTAINER_NAME is not defined in environment variables."
      );
    }

    const containerClient =
      this.blobServiceClient.getContainerClient(containerName);
    const blobClient = containerClient.getBlobClient(blobName);

    console.log(
      `Downloading blob ${blobName} from container ${containerName} to ${downloadPath}`
    );
    await blobClient.downloadToFile(downloadPath);
    console.log(`Blob downloaded successfully to ${downloadPath}`);
  }
}
