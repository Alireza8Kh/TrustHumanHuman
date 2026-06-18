data = table2array(Untitled);

% Find rows where first column equals -3300 or is NaN
rowsToRemove = (data(:,1) == -3300) | isnan(data(:,1));

% Remove those rows
data(rowsToRemove, :) = [];

% Number of samples (rows)
numSamples = size(data, 1);

% Sampling interval in ms (0.5 ms)
freq = 20000; %%Hz
dt = 1/freq * 10^3; %% ms

% Create time vector in ms
time_ms = (0:numSamples-1)' * dt;

% Plot sensor 1 data over time as example
figure;
plot(time_ms, data(:, 1));  % Assuming data from the 1st column (adjust as needed)
xlabel('Time (s)');
ylabel('Sensor Value');
title('Sensor Data Over Time');
grid on;
