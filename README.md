# 🚦 [Trafiko](https://stm-production.itch.io/trafiko)

Trafiko is an intelligent traffic light control system built in Unity, using **Reinforcement Learning (RL)** to train an agent that optimizes traffic flow at road intersections. The AI agent learns how to manage traffic light cycles in real-time based on dynamic traffic conditions, aiming to reduce waiting times, avoid congestion, and minimize emissions caused by vehicle idling.

---
![Trafiko Demo](Untitled.gif)


## 🧠 Description (EN)

Trafiko simulates vehicle traffic in a virtual environment with cars generated based on realistic time patterns, including low-traffic periods (e.g., nighttime). The agent uses **Proximal Policy Optimization (PPO)** to learn optimal traffic light control strategies through repeated interactions with the environment.

The system is designed with scalability in mind: future versions can support multiple communicating intersections, making it a viable solution for **smart city** infrastructure.

### 🏋️‍♂️ Reinforcement Learning Training Details

During training, the agent was rewarded based on its ability to improve traffic flow across the intersection. Specifically, the reward function was designed to:

- **Encourage clearing vehicles** from the intersection efficiently
- **Penalize high congestion** levels, especially during peak traffic
- **Minimize average vehicle waiting time**, promoting fairness and overall throughput

These reward signals help the agent learn a balanced traffic light control policy that dynamically adapts to varying traffic conditions while maintaining smooth flow and reducing unnecessary idling.
---
## 🛠️ Technologies Used

- **Unity** – Simulation environment and traffic visualization  
- **ML-Agents Toolkit** – Reinforcement Learning framework by Unity  
- **Python** – Backend for model training and reward processing  
- **Proximal Policy Optimization (PPO)** – The main algorithm used for training  
- **PyTorch** – The backbone for training  
- **C#** – For Unity-specific logic and integration  

---

## 📘 About Reinforcement Learning & PPO

### What is Reinforcement Learning?

![Neural Network](nn.gif)

Reinforcement Learning is a machine learning paradigm where an **agent** interacts with an **environment**, taking actions to maximize cumulative **reward**. In Trafiko's case, the agent controls traffic lights and receives rewards based on improved traffic flow, minimized wait times, and reduced congestion.

### Why PPO?

**Proximal Policy Optimization (PPO)** is a popular policy gradient method used in RL due to its balance between performance and stability. PPO helps prevent the agent from making drastic policy updates, which could destabilize learning. It's especially effective in continuous and complex environments like traffic simulation.

Key benefits:
- Sample-efficient
- Stable updates
- Robust to noise and delays

---

## 📄 License

MIT License

---

## 📫 Contact

For questions or collaborations, feel free to open an issue or reach out.

Email: stefanmoldoveanu2007@gmail.com

