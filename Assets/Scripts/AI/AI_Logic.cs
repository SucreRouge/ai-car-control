﻿using AIShared;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;

public static class AI_Logic {
    private static float _startTime;
    private static AI_Model model;
    private static List<AI_Evaluation> evaluationResults;

    public static void Start() {
        var initialResponse = new AI_EvaluationResponse {
            /* Initial parameters are all zeros. */
            Model = new AI_Model {
                Params = new List<float> {
                    0,0,0,0,
                    0,0,0,0,
                    0,0,0,0,
                    0,0,0,0,
                    0,0,0,0
                } 
            }
        };

        evaluationResults = new List<AI_Evaluation>();

        // Create initial model
        UpdateModel(initialResponse);

        _startTime = 0.0f;
    }

    public static void Restart(AI_Model newModel) {
        model = newModel;

        evaluationResults = new List<AI_Evaluation>();
        _startTime = 0.0f;
    }

    private static void UpdateModel(AI_EvaluationResponse response) {
        // Create a new AI model
        model = response.Model;
    }
    
    public static void UpdateModelJson(byte[] bytes) {
        AI_EvaluationResponse response;

        try {
            var json = System.Text.Encoding.UTF8.GetString(bytes);

            response = JsonUtility.FromJson<AI_EvaluationResponse>(json);

            if (response.Model == null || !ValidModel(response.Model)) {
                throw new System.Exception();
            }
        } catch {
            Debug.logger.LogWarning("AI_Logic [RunEvaluation]", "Cannot parse server response.");
            return;
        }

        Debug.logger.Log("Updating model...");

        // Update model
        Restart(response.Model);
    }

    private static bool ValidModel(AI_Model model) {
        if (model.Params == null || model.Params.Count != 20) {
            return false;
        }

        return true;
    }

    private static byte[] GetEvaluationRequestBody(float survivalTime) {
        var results = new AI_EvaluationRequest {
            SurvivalTime = survivalTime,
            EvaluationResults = evaluationResults,
            CurrentModel = model
        };

        var json = JsonUtility.ToJson(results);

        return System.Text.Encoding.UTF8.GetBytes(json);
    }   

    public static IEnumerator RunEvaluationCoroutine(System.Action resetCallBack) {
        var survivalTime = Time.time - _startTime;

        Debug.logger.Log("Crashed. Processing results... survival time: " + survivalTime);

        // Process the results and setup a new test                        
        UploadHandler uploader = new UploadHandlerRaw(AI_Logic.GetEvaluationRequestBody(survivalTime));
        uploader.contentType = "application/json";
        var downloader = new DownloadHandlerBuffer();
        var wr = new UnityWebRequest("http://localhost:8888/sendEvaluation/", "POST", downloader, uploader);
        yield return wr.Send();
        
        if (downloader.isDone) {
            // Update the AI model based on server response
            UpdateModelJson(downloader.data);
        } else {
            Debug.logger.LogWarning("AI_Logic [RunEvaluation]", "Invalid server response.");
        }

        Debug.logger.Log("Restarting...");        
        resetCallBack();
    }

    private static byte[] GetRunAIRequestBody(float time, float deltaTime, AI_Input input) {
        var request = new AI_InputRequest {
            Time = time,
            DeltaTime = deltaTime,
            Input = input
        };

        var json = JsonUtility.ToJson(request);

        return System.Text.Encoding.UTF8.GetBytes(json);
    }

    private static AI_Output GetRunOutput(byte[] bytes) {
        AI_InputResponse response;

        try {
            var json = System.Text.Encoding.UTF8.GetString(bytes);

            response = JsonUtility.FromJson<AI_InputResponse>(json);

            if (response.Output == null) {
                throw new System.Exception();
            }
        } catch {
            Debug.logger.LogWarning("AI_Logic [RunAI]", "Cannot parse server response.");
            return new AI_Output {
                Steering = 0,
                Acceleration = 0,
                Footbrake = 0,
                Handbrake = 0
            };
        }

        // Update model
        return response.Output;
    }

    public static IEnumerator RunAICoroutine(float time, float deltaTime, AI_Input input, System.Action<AI_Output> callBack) {
        // Process the results and setup a new test                        
        UploadHandler uploader = new UploadHandlerRaw(AI_Logic.GetRunAIRequestBody(time, deltaTime, input));
        uploader.contentType = "application/json";
        var downloader = new DownloadHandlerBuffer();
        var wr = new UnityWebRequest("http://localhost:8888/handleInput/", "POST", downloader, uploader);
        yield return wr.Send();
        
        if (downloader.isDone) {
            // Update the AI model based on server response
            var output = GetRunOutput(downloader.data);

            // Move the car
            callBack(output);

            // Save evaluation
            evaluationResults.Add(new AI_Evaluation {
                time = time,
                input = input,
                output = output
            });
        } else {
            Debug.logger.LogWarning("AI_Logic [RunAI]", "Invalid server response.");
        }
    }
}