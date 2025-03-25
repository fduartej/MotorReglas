import { Request, Response } from "express";
import { RuleEngineService } from "../services/RuleEngineService";

export class IndexController {
  private ruleEngineService: RuleEngineService;

  constructor() {
    this.ruleEngineService = new RuleEngineService();
  }

  public async handleRequest(req: Request, res: Response): Promise<void> {
    try {
      console.log("Request received:", req.body);

      const { decision, modelo, inputData } = req.body;

      if (!decision || !modelo || !inputData) {
        res.status(400).json({
          error: "Missing required parameters: decision, modelo, or inputData",
        });
        return;
      }

      // Delegate the logic to RuleEngineService
      const evaluation = await this.ruleEngineService.processRequest(
        decision,
        modelo,
        inputData
      );

      console.log("evaluation result:", evaluation);

      res.json({ success: true, evaluation });
    } catch (error: any) {
      console.error("An error occurred:", error.message);
      res.status(500).json({ error: error.message });
    }
  }

  public async cleanLocalFiles(req: Request, res: Response): Promise<void> {
    try {
      await this.ruleEngineService.cleanLocalDirectory();
      res.json({ success: true, message: "Local directory cleaned." });
    } catch (error: any) {
      console.error("An error occurred:", error.message);
      res.status(500).json({ error: error.message });
    }
  }
}
