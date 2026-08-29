using Microsoft.ML;
using Microsoft.ML.Trainers.FastTree;

namespace TaskPriorityApi.ML;

public static class ModelTrainer
{
    public static (ITransformer Model, double RSquared) TrainAndSave(MLContext ml, string modelPath, int trainingRows = 800)
    {
        var data = ml.Data.LoadFromEnumerable(SyntheticDataGenerator.Generate(trainingRows));
        var split = ml.Data.TrainTestSplit(data, testFraction: 0.2);

        var pipeline = ml.Transforms.Concatenate("Features",
                nameof(PriorityData.DeadlineDays),
                nameof(PriorityData.AssigneeKnown),
                nameof(PriorityData.TagCount),
                nameof(PriorityData.HasUrgentTag),
                nameof(PriorityData.TitleLength))
            .Append(ml.Regression.Trainers.FastTree(
                labelColumnName: nameof(PriorityData.Score),
                featureColumnName: "Features",
                numberOfTrees: 100,
                numberOfLeaves: 16,
                minimumExampleCountPerLeaf: 5));

        var model = pipeline.Fit(split.TrainSet);
        var metrics = ml.Regression.Evaluate(model.Transform(split.TestSet), labelColumnName: nameof(PriorityData.Score));

        var directory = Path.GetDirectoryName(modelPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        ml.Model.Save(model, split.TrainSet.Schema, modelPath);

        return (model, metrics.RSquared);
    }
}
