import torch
from mlagents.torch_utils import torch as mla_torch
from mlagents.trainers.torch_entities.networks import SimpleActor
from mlagents.trainers.torch_entities.model_serialization import ModelSerializer, exporting_to_onnx, TensorNames
from mlagents.trainers.settings import NetworkSettings, SerializationSettings
from mlagents_envs.base_env import ActionSpec, BehaviorSpec
from mlagents_envs.base_env import ObservationSpec, ObservationType, DimensionProperty

cp = torch.load('results/stairs_curr_01/StairsAgent/StairsAgent-412456.pt', map_location='cpu')

obs_specs = [
    ObservationSpec(shape=(45,), dimension_property=(DimensionProperty.NONE,), observation_type=ObservationType.DEFAULT, name="rays"),
    ObservationSpec(shape=(5,), dimension_property=(DimensionProperty.NONE,), observation_type=ObservationType.DEFAULT, name="vector"),
]
action_spec = ActionSpec(2, ())
behavior_spec = BehaviorSpec(obs_specs, action_spec)
network = NetworkSettings(normalize=True, hidden_units=128, num_layers=2)

actor = SimpleActor(obs_specs, network, action_spec)
actor.load_state_dict(cp['Policy'])
actor.eval()

# Criar objecto fake com os atributos que ModelSerializer precisa
class FakePolicy:
    pass

policy = FakePolicy()
policy.actor = actor
policy.behavior_spec = behavior_spec
policy.export_memory_size = 0

serializer = ModelSerializer(policy)
output_path = 'results/stairs_curr_01/StairsAgent/StairsAgent'
serializer.export_policy_model(output_path)
print('Exportado com sucesso em', output_path + '.onnx')