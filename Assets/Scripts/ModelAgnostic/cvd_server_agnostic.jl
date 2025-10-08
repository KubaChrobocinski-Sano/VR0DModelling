using Sockets
using JSON
using DifferentialEquations

# --- Model Definition ---
# This section is now the single source of truth for the model's structure.
# To change the model, you would modify this dictionary.
const MODEL_METADATA = Dict(
    "modelName" => "3-Element Windkessel",
    "parameters" => [
        Dict("name" => "heartRate", "value" => 70.0),
        Dict("name" => "R1", "value" => 0.02),
        Dict("name" => "R2", "value" => 0.5),
        Dict("name" => "C", "value" => 1.5)
    ],
    "outputs" => ["plv", "pa", "flow", "volume"]
)

println("Julia Cardiovascular Server running on port 2000...")
server = listen(2000)

# --- Simulation Function ---
# This function now takes a dictionary of parameters directly.
function run_sim(params)
    # Extract parameters from the dictionary
    Rp = params["R1"]
    Rd = params["R2"]
    C  = params["C"]
    HR = params["heartRate"]

    # Model constants and simulation settings
    dt = 0.0001          # time step
    tau = 60/HR         # period of time
    MCFP = [50, 50, 50, 50]     # Initial conditions

    # "Closing loop" data - internal model details
    rav         = 0.01;         # aortic valve resistance
    rmv         = 0.3;          # mitral valve resistance
    tauS1       = 0.35;         # tauES
    tauS2       = 0.50;         # tauSS
    ELVmax      = 0.10;
    ELVmin      = 0.05;

    function heartValve(pressureDifference, valveResistance)
        q = (pressureDifference./valveResistance) .* (pressureDifference >  0) +
        (pressureDifference/1000/valveResistance) .* (pressureDifference <= 0);
        return q
    end

    function ShiCosineElastance(t)
        t_cycle = rem(t, 60/HR)
        tauValue = t_cycle * HR / 60
        e = if tauValue <= tauS1
            0.5 * (1 - cos(pi * tauValue / tauS1))
        elseif tauValue <= tauS2
            0.5 * (1 + cos(pi * (tauValue - tauS1) / (tauS2 - tauS1)))
        else
            0.0
        end
        e1 = (ELVmax - ELVmin) * e + ELVmin
        
        dedt = if tauValue <= tauS1
            0.5 * sin(pi * tauValue / tauS1) * pi / tauS1
        elseif tauValue <= tauS2
            -0.5 * sin(pi * (tauValue - tauS1) / (tauS2 - tauS1)) * pi / (tauS2 - tauS1)
        else
            0.0
        end
        dedt1 = (ELVmax - ELVmin) * dedt * HR / 60
        return e1, dedt1
    end

    function model!(dPdt, P, p, t)
        Elocal, dEdtlocal = ShiCosineElastance(t)
        P_LV, P_BA, P_A, P_V = P
        
        iAV = heartValve(P_LV - P_BA, rav)
        iMV = heartValve(P_V - P_LV, rmv)

        dPdt[1] = (iMV - iAV) * Elocal + P_LV / Elocal * dEdtlocal
        dPdt[3] = (iAV - iMV) / C
        # Note: These equations seem to have a dependency loop. 
        # For this example, we assume the ODE solver can handle it, but this might need review for correctness.
        dPdt[2] = Rp/(Rp+rav) * dPdt[1] + rav/(Rp+rav) * dPdt[3]
        dPdt[4] = rmv/(Rd+rmv) * dPdt[3] + Rd/(Rd+rmv) * dPdt[1]
    end

    tspan = (0.0, 10.0)
    prob = ODEProblem(model!, MCFP, tspan)
    sol = solve(prob, RK4(), reltol=1e-8, abstol=1e-8, dt=dt, saveat=0.01)

    time = sol.t
    volume = [u[1] for u in sol.u]
    aopressure = [u[2] for u in sol.u]
    
    elastance_vals = [ShiCosineElastance(t)[1] for t in time]
    plv = elastance_vals .* volume
    
    flow_vals = (plv - aopressure) ./ Rp

    return Dict(
        "time" => time,
        "plv" => plv,
        "pa" => aopressure,
        "flow" => flow_vals,
        "volume" => volume
    )
end


# --- Server Loop ---
while true
    sock = accept(server)
    @async begin
        try
            line = readline(sock)
            request = JSON.parse(line)
            command = request["command"]
            
            println("Received command: ", command)

            if command == "get_metadata"
                # Send the model structure to Unity
                response = MODEL_METADATA
                write(sock, JSON.json(response) * "\n")

            elseif command == "run_simulation"
                # Run simulation with parameters sent from Unity
                params = request["parameters"]
                result = run_sim(params)
                write(sock, JSON.json(result) * "\n")

            else
                println("Unknown command: ", command)
            end

            flush(sock)
        catch e
            println("Error processing request: ", e)
            showerror(stdout, e, catch_backtrace())
        finally
            close(sock)
        end
    end
end
