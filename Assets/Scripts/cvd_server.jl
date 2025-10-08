using Sockets
using JSON
using DifferentialEquations

println("Julia Cardiovascular Server running on port 2000...")

server = listen(2000)

function run_sim(params)
    Rp = params["R1"]
    Rd = params["R2"]
    C  = params["C"]
    HR = params["heartRate"]
    dt = 0.0001;          # time step
    tau = 60/HR;         # period of time
    tRange = 0:dt:20;    # time range
    MCFP = [50, 50, 50, 50];     # average integrated pressure throughout the circulatory system

    #"Closing loop" data
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

# Elastance function
    function ShiCosineElastance(t, tauESvalue, tauSSvalue, ELVmaxValue, ELVminValue)
        t = rem(t,60/HR);
        tauValue = t * HR / 60;
        e = 1/2*(1-cos((pi*tauValue)/tauESvalue)) .* (tauValue >= 0) .* (tauValue <= tauESvalue) +
        1/2*(1+cos(pi*(tauValue-tauESvalue)/(tauSSvalue-tauESvalue))).* (tauValue > tauESvalue) .* (tauValue <= tauSSvalue) +
        0 .* (tauValue > tauSSvalue);
        e1 = (ELVmaxValue-ELVminValue) .* e + ELVminValue;
        dedt = 1/2 * sin(tauValue/tauESvalue * pi) * pi/tauESvalue .*(tauValue<=tauESvalue) + 
        -1/2*sin((tauValue-tauESvalue)/(tauSSvalue-tauESvalue)*pi)*pi/(tauSSvalue-tauESvalue).*(tauValue> tauESvalue).*(tauValue<=tauSSvalue) +
        0;
        dedt1 = (ELVmaxValue-ELVminValue) * dedt * HR / 60;
        return e1, dedt1
    end

# Model function
    function model(dPdt:: Vector, P:: Vector,p, t)
        Elocal, dEdtlocal = ShiCosineElastance(t, tauS1, tauS2, ELVmax, ELVmin)
        P_LV = P[1]
        P_BA = P[2]
        P_A = P[3]
        P_V = P[4]
        iAV = heartValve(P_LV-P_BA,rav);       #flow through the aortic valve
        iMV = heartValve(P_V-P_LV,rmv);       #flow through the mitral valve

        dPdt[1] = (iMV-iAV) * Elocal + P_LV/Elocal * dEdtlocal
        dPdt[3] = 1/C * (iAV-iMV)
        dPdt[2] = Rp/(Rp+rav) * dPdt[1] + rav/(Rp+rav) * dPdt[3]
        dPdt[4] = rmv/(Rd+rmv) * dPdt[3] + Rd/(Rd+rmv) * dPdt[1]
        return P,t
    end

    tspan = (0.0,10.0)
    y0 = MCFP

    prob = ODEProblem(model,MCFP,tspan)
    sol = solve(prob, RK4(), reltol = 1e-8, abstol = 1e-8, dt=dt)

    time = collect(sol.t)
    volume = [u[1] for u in sol.u]
    aopressure = [u[2] for u in sol.u]
    #elast = [elastance(t) for t in sol.t]
    #plv = [elast[i] * (volume[i]) for i in eachindex(time)]
    #flow = [(plv[i] - aopressure[i]) / R1 for i in eachindex(time)]

    return Dict(
        "time" => time,
        #"plv" => plv,
        "pa" => aopressure,
        #"flow" => flow,
        "volume" => volume
    )
end

while true
    sock = accept(server)
    @async begin
        try
            line = readline(sock)
            if startswith(line, "SET")
                _, name, val = split(line)
                if name == "R1"
                    global R1 = parse(Float64, val)
                elseif name == "R2"
                    global R2 = parse(Float64, val)
                elseif name == "C1"
                global C1 = parse(Float64, val)
                end
                println("Updated: $name = $val")
            end
            params = JSON.parse(line)
            println("Received from Unity: ", params)
            result = run_sim(params)
            write(sock, JSON.json(result) * "\n")
            flush(sock)
            close(sock)
        catch e
            println("Error: ", e)
        end
    end
end
